using Application.Shared.Data;
using Application.Shared.Models;
using Application.Shared.Models.User;
using Application.Shared.Services.Org;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services.Data;

public class DatasetSharingService : IDatasetSharingService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailNotificationService _emailNotificationService;

    public DatasetSharingService(
        ApplicationDbContext context,
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        IEmailNotificationService emailNotificationService)
    {
        _context = context;
        _userService = userService;
        _userManager = userManager;
        _emailNotificationService = emailNotificationService;
    }

    public async Task<List<DatasetUserDto>> GetDatasetUsersAsync(string datasetId)
    {
        // var datasetUsers = await _context.DatasetUser
        //     .Include(du => du.User)
        //     .Where(du => du.DatasetId == datasetId)
        //     .Select(du => new DatasetUserDto
        //     {
        //         UserId = du.UserId,
        //         Email = du.User!.Email!,
        //         UserName = du.User!.UserName!,
        //         Type = du.Type,
        //         CreatedAt = du.CreatedAt
        //     })
        //     .OrderBy(du => du.Email)
        //     .ToListAsync();

        var datasetUsers = await _context.DatasetUser
            .Where(du => du.DatasetId == datasetId)
            .Select(du => new DatasetUserDto
            {
                UserId = du.UserId,
                Type = du.Type,
                CreatedAt = du.CreatedAt
            })
            .ToListAsync();

        // Per-user table scopes (empty = all tables).
        var tableRows = await _context.DatasetUserTable
            .Where(t => t.DatasetId == datasetId)
            .Select(t => new { t.UserId, t.TableName })
            .ToListAsync();
        var tablesByUser = tableRows
            .GroupBy(t => t.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TableName).OrderBy(n => n).ToList());

        // Populate user details using IUserService
        foreach (var du in datasetUsers)
        {
            var user = await _userService.GetUser(du.UserId);
            if (user != null)
            {
                du.Email = user.Email;
                du.UserName = user.UserName;
            }
            if (tablesByUser.TryGetValue(du.UserId, out var tables))
                du.Tables = tables;
        }

        return datasetUsers;
    }

    public async Task<bool> ShareDatasetAsync(ShareDatasetRequest request, string sharedByUserId)
    {
        try
        {
            // Check if dataset exists
            var dataset = await _context.Dataset.FindAsync(request.DatasetId);
            if (dataset == null)
                return false;

            // Find user by email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return false;

            // Check if user is already shared with this dataset
            var existingShare = await _context.DatasetUser
                .FirstOrDefaultAsync(du => du.DatasetId == request.DatasetId && du.UserId == user.Id);

            if (existingShare != null)
            {
                // Update existing share
                existingShare.Type = request.UserType;
                existingShare.ModifiedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new share
                var datasetUser = new DatasetUser
                {
                    DatasetId = request.DatasetId,
                    UserId = user.Id,
                    Type = request.UserType,
                    CreatedAt = DateTime.UtcNow
                };

                _context.DatasetUser.Add(datasetUser);
            }

            // Replace the user's table scope. Null/empty Tables = full access (no restriction rows).
            var currentTableRows = await _context.DatasetUserTable
                .Where(t => t.DatasetId == request.DatasetId && t.UserId == user.Id)
                .ToListAsync();
            _context.DatasetUserTable.RemoveRange(currentTableRows);

            var scopedTables = (request.Tables ?? new List<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var tableName in scopedTables)
            {
                _context.DatasetUserTable.Add(new DatasetUserTable
                {
                    DatasetId = request.DatasetId,
                    UserId = user.Id,
                    TableName = tableName,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // Get the user who shared the dataset
            var sharedByUser = await _userManager.FindByIdAsync(sharedByUserId);

            // Send email notification (never throws — a mail failure must not fail the share).
            await _emailNotificationService.SendDatasetSharedNotificationAsync(
                request.Email,
                dataset.Id!,
                dataset.Name!,
                dataset.CompanyId,
                sharedByUser?.UserName ?? "Unknown User",
                request.UserType,
                scopedTables);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> GrantTableAccessAsync(GrantTableShareRequest request, string sharedByUserId)
    {
        try
        {
            var dataset = await _context.Dataset.FindAsync(request.DatasetId);
            if (dataset == null) return false;

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return false;

            var tableName = request.TableName.Trim();
            if (string.IsNullOrWhiteSpace(tableName)) return false;

            var existingShare = await _context.DatasetUser
                .FirstOrDefaultAsync(du => du.DatasetId == request.DatasetId && du.UserId == user.Id);

            if (existingShare == null)
            {
                // New share → restricted to just this table.
                _context.DatasetUser.Add(new DatasetUser
                {
                    DatasetId = request.DatasetId,
                    UserId = user.Id,
                    Type = request.UserType,
                    CreatedAt = DateTime.UtcNow
                });
                _context.DatasetUserTable.Add(new DatasetUserTable
                {
                    DatasetId = request.DatasetId,
                    UserId = user.Id,
                    TableName = tableName,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (existingShare.Type != DatasetUserType.Admin)
            {
                var rows = await _context.DatasetUserTable
                    .Where(t => t.DatasetId == request.DatasetId && t.UserId == user.Id)
                    .ToListAsync();

                // rows.Count == 0 means the user already has full access — don't downgrade them to one table.
                if (rows.Count > 0 && !rows.Any(r => string.Equals(r.TableName, tableName, StringComparison.OrdinalIgnoreCase)))
                {
                    _context.DatasetUserTable.Add(new DatasetUserTable
                    {
                        DatasetId = request.DatasetId,
                        UserId = user.Id,
                        TableName = tableName,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            // existingShare.Type == Admin → already full access; nothing to do.

            await _context.SaveChangesAsync();

            var sharedByUser = await _userManager.FindByIdAsync(sharedByUserId);
            await _emailNotificationService.SendDatasetSharedNotificationAsync(
                request.Email,
                dataset.Id!,
                dataset.Name!,
                dataset.CompanyId,
                sharedByUser?.UserName ?? "Unknown User",
                request.UserType,
                new List<string> { tableName });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> UpdateDatasetUserTypeAsync(string datasetId, string userId, DatasetUserType userType)
    {
        try
        {
            var datasetUser = await _context.DatasetUser
                .FirstOrDefaultAsync(du => du.DatasetId == datasetId && du.UserId == userId);

            if (datasetUser == null)
                return false;

            datasetUser.Type = userType;
            datasetUser.ModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> RemoveDatasetUserAsync(string datasetId, string userId)
    {
        try
        {
            var datasetUser = await _context.DatasetUser
                .FirstOrDefaultAsync(du => du.DatasetId == datasetId && du.UserId == userId);

            if (datasetUser == null)
                return false;

            // No cascade deletes in this codebase — remove the user's table scopes explicitly first.
            var tableRows = await _context.DatasetUserTable
                .Where(t => t.DatasetId == datasetId && t.UserId == userId)
                .ToListAsync();
            _context.DatasetUserTable.RemoveRange(tableRows);

            _context.DatasetUser.Remove(datasetUser);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<List<Dataset>> GetSharedDatasetsAsync(string userId, string companyId)
    {
        var sharedDatasets = await _context.DatasetUser
            .Include(du => du.Dataset)
            .Where(du => du.UserId == userId && du.Dataset!.CompanyId == companyId)
            .Select(du => du.Dataset!)
            .ToListAsync();

        return sharedDatasets;
    }

    public async Task<bool> HasDatasetAccessAsync(string datasetId, string userId, DatasetUserType? requiredType = null)
    {
        var datasetUser = await _context.DatasetUser
            .FirstOrDefaultAsync(du => du.DatasetId == datasetId && du.UserId == userId);

        if (datasetUser == null)
            return false;

        if (requiredType == null)
            return true;

        // Check if user has required permission level
        // Admin (0) > Editor (1) > Viewer (2)
        return (int)datasetUser.Type <= (int)requiredType.Value;
    }
}

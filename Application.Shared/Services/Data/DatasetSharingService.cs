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
        var datasetUsers = await _context.DatasetUser
            .Include(du => du.User)
            .Where(du => du.DatasetId == datasetId)
            .Select(du => new DatasetUserDto
            {
                UserId = du.UserId,
                Email = du.User!.Email!,
                UserName = du.User!.UserName!,
                Type = du.Type,
                CreatedAt = du.CreatedAt
            })
            .OrderBy(du => du.Email)
            .ToListAsync();

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

            await _context.SaveChangesAsync();

            // Get the user who shared the dataset
            var sharedByUser = await _userManager.FindByIdAsync(sharedByUserId);
            
            // Send email notification
            await _emailNotificationService.SendDatasetSharedNotificationAsync(
                request.Email,
                dataset.Name!,
                sharedByUser?.UserName ?? "Unknown User",
                request.UserType);

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

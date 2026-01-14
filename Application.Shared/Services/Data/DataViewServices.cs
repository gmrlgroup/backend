using Application.Shared.Data;
using Application.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services.Data;

public class CommentService : ICommentService
{
    private readonly ApplicationDbContext _context;
    

    public CommentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DataTableComment>> GetCommentsAsync(string datasetId, string tableName)
    {
        return await _context.Set<DataTableComment>()
            .Where(c => c.DatasetId == datasetId && c.TableName == tableName)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<DataTableComment> AddCommentAsync(DataTableComment comment)
    {
        comment.Id = Guid.NewGuid().ToString();
        comment.CreatedAt = DateTime.UtcNow;
        
        // Get user info (simplified - in real implementation you'd get from user service)
        comment.UserName = "Current User"; // TODO: Get from user service
        comment.UserEmail = "user@example.com"; // TODO: Get from user service
        
        _context.Set<DataTableComment>().Add(comment);
        await _context.SaveChangesAsync();
        
        return comment;
    }

    public async Task<bool> DeleteCommentAsync(string commentId, string userId)
    {
        var comment = await _context.Set<DataTableComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId);
        
        if (comment == null)
            return false;
        
        _context.Set<DataTableComment>().Remove(comment);
        await _context.SaveChangesAsync();
        
        return true;
    }

    public async Task<DataTableComment?> UpdateCommentAsync(string commentId, string content, string userId)
    {
        var comment = await _context.Set<DataTableComment>()
            .FirstOrDefaultAsync(c => c.Id == commentId && c.UserId == userId);
        
        if (comment == null)
            return null;
        
        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        return comment;
    }
}

public class UserPreferencesService : IUserPreferencesService
{
    private readonly ApplicationDbContext _context;

    public UserPreferencesService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserColumnPreferences?> GetUserColumnPreferencesAsync(string userId, string datasetId, string tableName)
    {
        // For now, return null to indicate no saved preferences
        // In a real implementation, you'd store these in the database
        await Task.CompletedTask;
        return null;
    }

    public async Task<UserColumnPreferences> SaveUserColumnPreferencesAsync(UserColumnPreferences preferences)
    {
        // For now, just return the preferences as-is
        // In a real implementation, you'd save these to the database
        await Task.CompletedTask;
        preferences.LastModified = DateTime.UtcNow;
        return preferences;
    }
}

public class UserSearchService : IUserSearchService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManagementDbContext _userContext;

    public UserSearchService(ApplicationDbContext context, UserManagementDbContext userContext)
    {
        _context = context;
        _userContext = userContext;
    }

    public async Task<List<UserMention>> SearchUsersAsync(string companyId, string searchTerm, int maxResults = 5)
    {
        // Get users from the company that match the search term
        var users = await _userContext.ApplicationUser
            .Where(u => (u.UserName!.Contains(searchTerm) || 
                        u.Email!.Contains(searchTerm) ||
                        (u.UserName != null && u.UserName.Contains(searchTerm))))
            .Take(maxResults)
            .Select(u => new UserMention
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                FullName = u.UserName ?? "", // TODO: Add FullName property to ApplicationUser
                Email = u.Email ?? ""
            })
            .ToListAsync();

        return users;
    }
}

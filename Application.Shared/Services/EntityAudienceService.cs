using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Models.User;
using Application.Shared.Services.Org;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class EntityAudienceService : IEntityAudienceService
{
    private readonly StatusDbContext _context;
    private readonly IUserService _userService;

    public EntityAudienceService(StatusDbContext context, IUserService userService)
    {
        _context = context;
        _userService = userService;
    }

    public async Task<List<EntityAudience>> GetAudienceForEntityAsync(string entityId, string companyId)
    {
        return await _context.EntityAudiences
            .Where(a => a.EntityId == entityId && a.CompanyId == companyId && a.IsActive)
            .OrderBy(a => a.AudienceType)
            .ThenBy(a => a.DisplayName)
            .ToListAsync();
    }

    public async Task<EntityAudience> AddAsync(string entityId, string applicationUserId, EntityAudienceType type, string companyId)
    {
        var user = await _userService.GetUser(applicationUserId)
            ?? throw new ArgumentException("User not found", nameof(applicationUserId));

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new InvalidOperationException("Selected user has no email address.");

        // Reuse an existing (possibly deactivated) row for the same entity+user+type instead of duplicating.
        var existing = await _context.EntityAudiences.FirstOrDefaultAsync(a =>
            a.EntityId == entityId &&
            a.ApplicationUserId == applicationUserId &&
            a.AudienceType == type &&
            a.CompanyId == companyId);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.Email = user.Email!;
            existing.DisplayName = user.UserName ?? user.Email;
            existing.ModifiedOn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        var audience = new EntityAudience
        {
            Id = Guid.NewGuid().ToString(),
            EntityId = entityId,
            ApplicationUserId = applicationUserId,
            Email = user.Email!,
            DisplayName = user.UserName ?? user.Email,
            AudienceType = type,
            IsActive = true,
            CompanyId = companyId,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow
        };

        _context.EntityAudiences.Add(audience);
        await _context.SaveChangesAsync();
        return audience;
    }

    public async Task<EntityAudience> UpdateAsync(EntityAudience audience)
    {
        audience.ModifiedOn = DateTime.UtcNow;
        _context.EntityAudiences.Update(audience);
        await _context.SaveChangesAsync();
        return audience;
    }

    public async Task<bool> RemoveAsync(string id, string companyId)
    {
        var audience = await _context.EntityAudiences
            .FirstOrDefaultAsync(a => a.Id == id && a.CompanyId == companyId);

        if (audience == null) return false;

        _context.EntityAudiences.Remove(audience);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ApplicationUser>> GetAssignableUsersAsync(string companyId)
    {
        return await _userService.GetUsers(companyId);
    }
}

using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Models.User;

namespace Application.Shared.Services;

public interface IEntityAudienceService
{
    Task<List<EntityAudience>> GetAudienceForEntityAsync(string entityId, string companyId);

    Task<EntityAudience> AddAsync(string entityId, string applicationUserId, EntityAudienceType type, string companyId);

    Task<EntityAudience> UpdateAsync(EntityAudience audience);

    Task<bool> RemoveAsync(string id, string companyId);

    Task<List<ApplicationUser>> GetAssignableUsersAsync(string companyId);
}

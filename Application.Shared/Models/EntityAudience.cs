using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// Links a user to a monitored entity with a role (Owner/Maintainer/Stakeholder).
/// Members of an entity's audience — and of its upstream entities' audiences — are
/// emailed when an incident is opened on that entity.
/// </summary>
[Table("entity_audience")]
public class EntityAudience : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    // Logical reference to ApplicationUser in the (separate) identity database — no FK.
    [Required]
    [MaxLength(450)]
    public string ApplicationUserId { get; set; } = string.Empty;

    // Email + display name are denormalized at assignment time so notifications never
    // need to reach across to the identity database (the scheduler has no access to it).
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? DisplayName { get; set; }

    public EntityAudienceType AudienceType { get; set; } = EntityAudienceType.Stakeholder;

    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }
}

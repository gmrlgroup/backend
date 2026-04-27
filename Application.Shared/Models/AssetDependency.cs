using Application.Shared.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models;

[Table("entity_dependency")]
public class AssetDependency : BaseModel
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    public string DependsOnEntityId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsCritical { get; set; } = false;

    public AssetType? DependencyType { get; set; }

    public int Order { get; set; } = 0;

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; } = null!;

    [JsonIgnore]
    [ForeignKey(nameof(DependsOnEntityId))]
    public virtual MonitoredAsset? DependsOnEntity { get; set; } = null!;
}

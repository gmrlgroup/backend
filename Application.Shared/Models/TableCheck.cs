using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models;

/// <summary>
/// Data-freshness check configuration for a Table-type entity (MonitoredAsset). When enabled,
/// AssetPingJob reads <c>MAX([FreshnessColumn])</c> and <c>COUNT(*)</c> (read-only) from the table
/// through the parent Database entity's <see cref="DatabaseConnection"/> and flags the table as
/// Degraded when the newest row is older than <see cref="MaxAgeMinutes"/>. One row per entity.
/// </summary>
[Table("entity_table_check")]
public class TableCheck : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>The timestamp column inspected for freshness (e.g. "updated_at").</summary>
    [MaxLength(200)]
    public string? FreshnessColumn { get; set; }

    /// <summary>Maximum allowed age of the newest row, in minutes, before the table is considered stale.</summary>
    public int MaxAgeMinutes { get; set; }

    public bool IsEnabled { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }
}

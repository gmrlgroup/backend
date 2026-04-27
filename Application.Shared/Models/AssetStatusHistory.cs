using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

[Table("entity_status_history")]
public class AssetStatusHistory : BaseModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    public AssetStatus Status { get; set; }

    [MaxLength(2000)]
    public string? StatusMessage { get; set; }

    public double? ResponseTime { get; set; }

    public double? UptimePercentage { get; set; }

    public DateTime? CheckedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    [JsonIgnore]
    public virtual MonitoredAsset? Entity { get; set; } = null!;
}

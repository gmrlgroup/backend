using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models;

/// <summary>
/// Links a Dataset-type <see cref="MonitoredAsset"/> to a <see cref="PowerBiConnection"/> and the
/// specific Power BI workspace + dataset it represents, so refresh history can be read and refreshes
/// triggered. One link per entity.
/// </summary>
[Table("power_bi_dataset_link")]
public class PowerBiDatasetLink : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    public string PowerBiConnectionId { get; set; } = string.Empty;

    /// <summary>Power BI workspace (group) id that hosts the dataset.</summary>
    [Required]
    [MaxLength(100)]
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>Power BI dataset id.</summary>
    [Required]
    [MaxLength(100)]
    public string DatasetId { get; set; } = string.Empty;

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(PowerBiConnectionId))]
    public virtual PowerBiConnection? Connection { get; set; }
}

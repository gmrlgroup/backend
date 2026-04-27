using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;


[Table("entity")]
public class MonitoredAsset : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public AssetType EntityType { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(100)]
    public string? Version { get; set; }

    [MaxLength(200)]
    public string? Owner { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsCritical { get; set; } = false;

    public string? Group { get; set; } = "Default";

    [MaxLength(4000)]
    public string? Metadata { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<AssetDependency>? Dependencies { get; set; } = new List<AssetDependency>();
    public virtual ICollection<AssetDependency>? DependentOn { get; set; } = new List<AssetDependency>();

    public virtual ICollection<MonitoringJob> Jobs { get; set; } = new List<MonitoringJob>();

    public virtual ICollection<AssetStatusHistory> StatusHistory { get; set; } = new List<AssetStatusHistory>();

    [JsonIgnore]
    public virtual ICollection<Incident> Incidents { get; set; } = new List<Incident>();

    public string GetEntityTypeClass()
    {
        return EntityType switch
        {
            AssetType.Server => "bg-green-100 text-green-800",
            AssetType.Database => "bg-blue-100 text-blue-800",
            AssetType.Report => "bg-purple-100 text-purple-800",
            AssetType.Dataset => "bg-yellow-100 text-yellow-800",
            AssetType.DataPipeline => "bg-indigo-100 text-indigo-800",
            AssetType.Table => "bg-orange-100 text-orange-800",
            AssetType.DataJob => "bg-red-100 text-red-800",
            _ => "bg-gray-100 text-gray-800"
        };
    }

    public string GetEntityTypeIcon()
    {
        return EntityType switch
        {
            AssetType.Server => "🖥️",
            AssetType.Database => "🗄️",
            AssetType.Report => "📊",
            AssetType.Dataset => "📈",
            AssetType.DataPipeline => "🔄",
            AssetType.Table => "📋",
            AssetType.DataJob => "⚙️",
            _ => "📁"
        };
    }
}

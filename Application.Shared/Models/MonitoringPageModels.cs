using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models;

public class MonitoringPage : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [MaxLength(7)]
    public string? ThemeColor { get; set; } = "#6366f1";

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(2000)]
    public string? HeaderMessage { get; set; }

    [MaxLength(2000)]
    public string? FooterMessage { get; set; }

    public int RefreshIntervalSeconds { get; set; } = 30;

    public bool ShowUptime { get; set; } = true;
    public bool ShowResponseTime { get; set; } = true;
    public bool ShowDependencies { get; set; } = true;

    [MaxLength(4000)]
    public string? DisplayConfig { get; set; }

    public virtual ICollection<MonitoringPageAsset> MonitoringPageAssets { get; set; } = new List<MonitoringPageAsset>();
}

public class MonitoringPageAsset : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string MonitoringPageId { get; set; } = string.Empty;

    [Required]
    public string EntityId { get; set; } = string.Empty;

    public int DisplayOrder { get; set; } = 0;

    public bool IsVisible { get; set; } = true;

    [MaxLength(100)]
    public string? GroupName { get; set; }

    [ForeignKey(nameof(MonitoringPageId))]
    public virtual MonitoringPage MonitoringPage { get; set; } = null!;

    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset Entity { get; set; } = null!;
}

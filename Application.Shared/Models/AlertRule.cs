using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models;

public class AlertRule : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public string? EntityId { get; set; }

    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Conditions { get; set; } = string.Empty;

    [Required]
    public AlertSeverity Severity { get; set; }

    public bool IsActive { get; set; } = true;

    public bool SendEmail { get; set; } = true;
    public bool SendSms { get; set; } = false;
    public bool SendWebhook { get; set; } = false;

    [MaxLength(1000)]
    public string? EmailRecipients { get; set; }

    [MaxLength(500)]
    public string? SmsRecipients { get; set; }

    [MaxLength(500)]
    public string? WebhookUrl { get; set; }

    public int CooldownMinutes { get; set; } = 15;

    public DateTime? LastTriggered { get; set; }

    public virtual ICollection<AlertInstance> AlertInstances { get; set; } = new List<AlertInstance>();
}

public enum AlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

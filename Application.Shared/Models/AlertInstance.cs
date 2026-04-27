using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models;

public class AlertInstance : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string AlertRuleId { get; set; } = string.Empty;

    [ForeignKey(nameof(AlertRuleId))]
    public virtual AlertRule AlertRule { get; set; } = null!;

    [Required]
    public DateTime TriggeredAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public bool IsResolved { get; set; } = false;

    [MaxLength(2000)]
    public string? Message { get; set; }

    [MaxLength(4000)]
    public string? Details { get; set; }

    [MaxLength(2000)]
    public string? TriggerData { get; set; }

    public bool EmailSent { get; set; } = false;
    public bool SmsSent { get; set; } = false;
    public bool WebhookSent { get; set; } = false;

    public DateTime? EmailSentAt { get; set; }
    public DateTime? SmsSentAt { get; set; }
    public DateTime? WebhookSentAt { get; set; }

    [MaxLength(1000)]
    public string? NotificationErrors { get; set; }
}

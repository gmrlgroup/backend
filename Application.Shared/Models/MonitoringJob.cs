using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

[Table("job")]
public class MonitoringJob : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public MonitoringJobType JobType { get; set; }

    [Required]
    public TriggerType TriggerType { get; set; }

    [Required]
    public MonitoringJobStatus Status { get; set; } = MonitoringJobStatus.Pending;

    public string? EntityId { get; set; }

    [ForeignKey(nameof(EntityId))]
    [JsonIgnore]
    public virtual MonitoredAsset? Entity { get; set; }

    [MaxLength(100)]
    public string? CronExpression { get; set; }

    [MaxLength(2000)]
    public string? SensorConfig { get; set; }

    [MaxLength(4000)]
    public string? Command { get; set; }

    public int TimeoutSeconds { get; set; } = 300;

    public int MaxRetries { get; set; } = 3;
    public int RetryIntervalSeconds { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? LastSuccessTime { get; set; }

    [MaxLength(2000)]
    public string? LastResult { get; set; }

    [MaxLength(4000)]
    public string? LastError { get; set; }

    public double? SuccessRate { get; set; }

    public double? AverageExecutionTime { get; set; }

    public virtual ICollection<MonitoringJobExecution> JobExecutions { get; set; } = new List<MonitoringJobExecution>();
}

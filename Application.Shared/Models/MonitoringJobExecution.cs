using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Application.Shared.Enums;

namespace Application.Shared.Models;

[Table("job_execution")]
public class MonitoringJobExecution : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string JobId { get; set; } = string.Empty;

    [ForeignKey(nameof(JobId))]
    public virtual MonitoringJob Job { get; set; } = null!;

    [Required]
    public MonitoringJobStatus Status { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public double? ExecutionTimeSeconds { get; set; }

    [MaxLength(4000)]
    public string? Result { get; set; }

    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }

    [MaxLength(2000)]
    public string? Output { get; set; }

    public int? ExitCode { get; set; }

    public int RetryAttempt { get; set; } = 0;

    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    [MaxLength(2000)]
    public string? Metadata { get; set; }
}

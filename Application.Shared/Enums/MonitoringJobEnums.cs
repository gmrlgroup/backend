using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum MonitoringJobType
{
    [Display(Name = "Health Check")]
    HealthCheck = 1,

    [Display(Name = "Status Monitor")]
    StatusMonitor = 2,

    [Display(Name = "Performance Check")]
    PerformanceCheck = 3,

    [Display(Name = "Connectivity Test")]
    ConnectivityTest = 4,

    [Display(Name = "Data Quality Check")]
    DataQualityCheck = 5,

    [Display(Name = "Custom Script")]
    CustomScript = 6
}

public enum TriggerType
{
    [Display(Name = "Cron Schedule")]
    Cron = 1,

    [Display(Name = "Sensor")]
    Sensor = 2,

    [Display(Name = "Manual")]
    Manual = 3,

    [Display(Name = "Event Based")]
    EventBased = 4
}

public enum MonitoringJobStatus
{
    [Display(Name = "Pending")]
    Pending = 0,

    [Display(Name = "Running")]
    Running = 1,

    [Display(Name = "Completed")]
    Completed = 2,

    [Display(Name = "Failed")]
    Failed = 3,

    [Display(Name = "Cancelled")]
    Cancelled = 4,

    [Display(Name = "Paused")]
    Paused = 5
}

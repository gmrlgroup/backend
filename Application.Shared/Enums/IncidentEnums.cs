using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum IncidentSeverity
{
    [Display(Name = "Low")]
    Low = 1,

    [Display(Name = "Medium")]
    Medium = 2,

    [Display(Name = "High")]
    High = 3,

    [Display(Name = "Critical")]
    Critical = 4
}

public enum IncidentStatus
{
    [Display(Name = "Open")]
    Open = 1,

    [Display(Name = "Investigating")]
    Investigating = 2,

    [Display(Name = "Identified")]
    Identified = 3,

    [Display(Name = "Monitoring")]
    Monitoring = 4,

    [Display(Name = "Resolved")]
    Resolved = 5
}

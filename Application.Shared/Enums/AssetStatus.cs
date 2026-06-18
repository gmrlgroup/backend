using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum AssetStatus
{
    [Display(Name = "Unknown")]
    Unknown = 0,

    [Display(Name = "Online")]
    Online = 1,

    [Display(Name = "Offline")]
    Offline = 2,

    [Display(Name = "Degraded")]
    Degraded = 3,

    [Display(Name = "Maintenance")]
    Maintenance = 4,

    [Display(Name = "Error")]
    Error = 5
}

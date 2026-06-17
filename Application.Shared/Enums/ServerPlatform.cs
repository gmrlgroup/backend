using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum ServerPlatform
{
    [Display(Name = "Windows")]
    Windows = 1,

    [Display(Name = "Linux")]
    Linux = 2,
}

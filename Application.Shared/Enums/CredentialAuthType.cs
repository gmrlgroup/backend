using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Enums;

public enum CredentialAuthType
{
    [Display(Name = "Password")]
    Password = 1,

    [Display(Name = "SSH Key")]
    SshKey = 2,
}

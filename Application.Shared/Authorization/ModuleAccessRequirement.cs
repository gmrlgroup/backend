using Microsoft.AspNetCore.Authorization;

namespace Application.Shared.Authorization;

/// <summary>
/// Authorization requirement satisfied when the user holds, for the active company, any of
/// <see cref="AllowedSuffixes"/> (or the company ADMIN role, which the handler always allows).
/// </summary>
public sealed class ModuleAccessRequirement : IAuthorizationRequirement
{
    public ModuleAccessRequirement(params string[] allowedSuffixes)
    {
        AllowedSuffixes = allowedSuffixes ?? Array.Empty<string>();
    }

    public string[] AllowedSuffixes { get; }
}

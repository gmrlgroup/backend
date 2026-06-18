using Microsoft.AspNetCore.Authorization;

namespace Application.Shared.Authorization;

/// <summary>
/// Grants a <see cref="ModuleAccessRequirement"/> when the authenticated user holds, for the
/// active company, the company ADMIN role or any of the requirement's allowed role suffixes.
/// Role claims are company-prefixed (e.g. <c>ACME_METRICS_READ</c>), and the active company is
/// a runtime value, so this cannot be expressed as a static <c>[Authorize(Roles = ...)]</c>.
/// </summary>
public sealed class ModuleAccessHandler : AuthorizationHandler<ModuleAccessRequirement>
{
    private readonly ICurrentCompanyAccessor _companyAccessor;

    public ModuleAccessHandler(ICurrentCompanyAccessor companyAccessor)
    {
        _companyAccessor = companyAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ModuleAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var companyId = _companyAccessor.GetCompanyId();
        if (string.IsNullOrWhiteSpace(companyId))
            return Task.CompletedTask;

        // ADMIN is implicitly allowed by every policy.
        if (context.User.IsInRole(RoleSuffixes.Role(companyId, RoleSuffixes.Admin)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        foreach (var suffix in requirement.AllowedSuffixes)
        {
            if (context.User.IsInRole(RoleSuffixes.Role(companyId, suffix)))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}

using Application.Shared.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Per-company application settings. ADMIN-only (<c>{companyId}_ADMIN</c>). Currently exposes the
/// debug-logging toggle that gates <see cref="Application.Shared.Services.Logging.IDebugLogService"/>
/// and controls whether the client debug-log side panel appears.
/// </summary>
[Route("api/company-settings")]
[ApiController]
[Authorize]
public class CompanySettingsController : ControllerBase
{
    private readonly ICompanySettingsService _settings;

    public CompanySettingsController(ICompanySettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<ActionResult<CompanySettingsDto>> Get(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Admin))
            return Forbid();

        var settings = await _settings.GetAsync(companyId, cancellationToken);
        return Ok(new CompanySettingsDto
        {
            DebugLoggingEnabled = settings.DebugLoggingEnabled,
            // Resolved rather than raw, so the settings UI always has a concrete pattern to preselect.
            ExportDateFormat = ExportDateFormats.Resolve(settings.ExportDateFormat),
        });
    }

    [HttpPut]
    public async Task<ActionResult<CompanySettingsDto>> Put(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        [FromBody] CompanySettingsDto body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Admin))
            return Forbid();
        if (body == null)
            return BadRequest("Settings body is required");

        // Reject an unknown pattern outright instead of silently substituting the default — otherwise a
        // client bug would look like a saved setting that quietly doesn't apply. Null means "unchanged".
        if (body.ExportDateFormat != null && !ExportDateFormats.IsAllowed(body.ExportDateFormat))
            return BadRequest($"'{body.ExportDateFormat}' is not a supported export date format.");

        var userId = Request.Headers["UserId"].FirstOrDefault();
        await _settings.SaveAsync(companyId, body, userId, cancellationToken);

        var saved = await _settings.GetAsync(companyId, cancellationToken);
        return Ok(new CompanySettingsDto
        {
            DebugLoggingEnabled = saved.DebugLoggingEnabled,
            ExportDateFormat = ExportDateFormats.Resolve(saved.ExportDateFormat),
        });
    }
}

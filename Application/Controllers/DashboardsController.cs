using Application.Shared.Authorization;
using Application.Shared.Models.Dashboards.Oos;
using Application.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = PolicyNames.DashboardsRead)]
public class DashboardsController : ControllerBase
{
    private readonly IOosDashboardService _oos;

    public DashboardsController(IOosDashboardService oos)
    {
        _oos = oos;
    }

    /// <summary>Out-of-Stock dashboard dataset for the company at a given as-of date.</summary>
    [HttpGet("oos")]
    public async Task<ActionResult<OosDashboardResponse>> GetOos(
        [FromHeader(Name = "X-Company-Id")] string? companyId,
        [FromQuery] DateTime? asOf = null,
        [FromQuery] int limit = 50000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required in headers");

        if (!User.HasCompanyRole(companyId, RoleSuffixes.DashboardsRead))
            return Forbid();

        var data = await _oos.GetAsync(companyId, asOf ?? DateTime.Today, limit, cancellationToken);
        return Ok(data);
    }
}

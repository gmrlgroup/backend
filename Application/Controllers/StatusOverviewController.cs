using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Application.Controllers;

/// <summary>
/// Read-only status board for any authenticated user (deliberately NOT gated behind the StatusRead
/// policy). Tenant scoping comes from the X-Company-Id header, consistent with the rest of the API.
/// </summary>
[Route("api/status/overview")]
[ApiController]
[Authorize]
public class StatusOverviewController : ControllerBase
{
    private readonly IStatusOverviewService _overviewService;

    public StatusOverviewController(IStatusOverviewService overviewService)
    {
        _overviewService = overviewService;
    }

    [HttpGet]
    public async Task<ActionResult<StatusOverviewDto>> GetOverview(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] int days,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _overviewService.GetOverviewAsync(companyId, days > 0 ? days : 30, ct));
    }

    [HttpGet("entities/{entityId}/day")]
    public async Task<ActionResult<IEnumerable<StatusDayEventDto>>> GetDayEvents(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        string entityId,
        [FromQuery] string date,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (string.IsNullOrEmpty(date)) return BadRequest("date query parameter is required");

        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dateUtc))
            return BadRequest("date must be a valid date (yyyy-MM-dd)");

        return Ok(await _overviewService.GetDayEventsAsync(companyId, entityId, dateUtc, ct));
    }
}

using Application.Shared.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Power BI connection management (company-scoped) plus per-dataset link config, refresh history,
/// and on-demand refresh. All endpoints require the X-Company-Id header.
/// </summary>
[Route("api/status")]
[ApiController]
[Authorize(Policy = PolicyNames.StatusRead)]
public class PowerBiController : ControllerBase
{
    private readonly IPowerBiConnectionService _connectionService;
    private readonly IPowerBiService _powerBiService;

    public PowerBiController(IPowerBiConnectionService connectionService, IPowerBiService powerBiService)
    {
        _connectionService = connectionService;
        _powerBiService = powerBiService;
    }

    // ---- Connections (company-scoped CRUD) ----

    [HttpGet("powerbi/connections")]
    public async Task<ActionResult<IEnumerable<PowerBiConnectionDto>>> GetConnections(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _connectionService.GetConnectionsAsync(companyId));
    }

    [HttpPost("powerbi/connections")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<PowerBiConnectionDto>> CreateConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, PowerBiConnectionRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var created = await _connectionService.CreateAsync(companyId, request, User?.Identity?.Name ?? "System");
        return Ok(created);
    }

    [HttpPut("powerbi/connections/{connectionId}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<PowerBiConnectionDto>> UpdateConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string connectionId, PowerBiConnectionRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var updated = await _connectionService.UpdateAsync(connectionId, companyId, request, User?.Identity?.Name ?? "System");
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("powerbi/connections/{connectionId}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string connectionId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return await _connectionService.DeleteAsync(connectionId, companyId) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // ---- Dataset link ----

    [HttpGet("entities/{entityId}/powerbi/link")]
    public async Task<ActionResult<PowerBiDatasetLinkDto>> GetLink(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var link = await _powerBiService.GetLinkAsync(entityId, companyId);
        return link == null ? NoContent() : Ok(link);
    }

    [HttpPut("entities/{entityId}/powerbi/link")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<PowerBiDatasetLinkDto>> SaveLink(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, PowerBiDatasetLinkRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _powerBiService.SaveLinkAsync(entityId, companyId, request, User?.Identity?.Name ?? "System"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("entities/{entityId}/powerbi/link")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteLink(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return await _powerBiService.DeleteLinkAsync(entityId, companyId) ? NoContent() : NotFound();
    }

    // ---- Refresh history + trigger ----

    [HttpGet("entities/{entityId}/powerbi/refreshes")]
    public async Task<ActionResult<IEnumerable<PowerBiRefreshDto>>> GetRefreshHistory(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        [FromQuery] int top, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _powerBiService.GetRefreshHistoryAsync(entityId, companyId, top > 0 ? top : 20, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PowerBiActionResult.Fail(ex.Message));
        }
    }

    [HttpGet("entities/{entityId}/powerbi/schedule")]
    public async Task<ActionResult<PowerBiRefreshScheduleDto>> GetRefreshSchedule(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            var schedule = await _powerBiService.GetRefreshScheduleAsync(entityId, companyId, ct);
            return schedule == null ? NoContent() : Ok(schedule);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PowerBiActionResult.Fail(ex.Message));
        }
    }

    [HttpPost("entities/{entityId}/powerbi/refresh")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<PowerBiActionResult>> TriggerRefresh(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var result = await _powerBiService.TriggerRefreshAsync(entityId, companyId, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ---- Lineage discovery ----

    [HttpGet("entities/{entityId}/powerbi/discover")]
    public async Task<ActionResult<PowerBiDiscoveryDto>> Discover(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _powerBiService.GetDiscoveryAsync(entityId, companyId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PowerBiActionResult.Fail(ex.Message));
        }
    }

    [HttpPost("entities/{entityId}/powerbi/lineage")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<PowerBiLineageCommitResult>> CommitLineage(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        PowerBiLineageCommitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _powerBiService.CommitLineageAsync(entityId, companyId, request, User?.Identity?.Name ?? "System", ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(PowerBiActionResult.Fail(ex.Message));
        }
    }
}

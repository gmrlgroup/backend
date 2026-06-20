using Application.Shared.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Per-entity database connection config plus table discovery/commit for Database-type entities.
/// All endpoints require the X-Company-Id header.
/// </summary>
[Route("api/status")]
[ApiController]
[Authorize(Policy = PolicyNames.StatusRead)]
public class DatabaseTableController : ControllerBase
{
    private readonly IDatabaseTableService _service;

    public DatabaseTableController(IDatabaseTableService service)
    {
        _service = service;
    }

    // ---- Connection ----

    [HttpGet("entities/{entityId}/database/connection")]
    public async Task<ActionResult<DatabaseConnectionDto>> GetConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var connection = await _service.GetConnectionAsync(entityId, companyId, ct);
        return connection == null ? NoContent() : Ok(connection);
    }

    [HttpPut("entities/{entityId}/database/connection")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<DatabaseConnectionDto>> SaveConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        DatabaseConnectionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _service.SaveConnectionAsync(entityId, companyId, request, User?.Identity?.Name ?? "System", ct));
    }

    [HttpDelete("entities/{entityId}/database/connection")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return await _service.DeleteConnectionAsync(entityId, companyId, ct) ? NoContent() : NotFound();
    }

    [HttpPost("entities/{entityId}/database/connection/test")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<DatabaseConnectionTestResult>> TestConnection(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _service.TestConnectionAsync(entityId, companyId, ct));
    }

    // ---- Tables ----

    [HttpGet("entities/{entityId}/database/tables")]
    public async Task<ActionResult<DatabaseTableDiscoveryDto>> DiscoverTables(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _service.DiscoverTablesAsync(entityId, companyId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new DatabaseConnectionTestResult { Ok = false, Error = ex.Message });
        }
    }

    [HttpPost("entities/{entityId}/database/tables")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<DatabaseTableCommitResult>> CommitTables(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        DatabaseTableCommitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        try
        {
            return Ok(await _service.CommitTablesAsync(entityId, companyId, request, User?.Identity?.Name ?? "System", ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new DatabaseConnectionTestResult { Ok = false, Error = ex.Message });
        }
    }
}

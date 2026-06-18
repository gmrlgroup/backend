using Application.Shared.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Application.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/status/incidents")]
[ApiController]
[Authorize(Policy = PolicyNames.StatusRead)]
public class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentsController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Incident>>> GetIncidents(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _incidentService.GetIncidentsAsync(companyId));
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<Incident>>> GetIncidentsPaged(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] IncidentQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _incidentService.GetIncidentsPagedAsync(companyId, parameters));
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Incident>>> GetActiveIncidents(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _incidentService.GetActiveIncidentsAsync(companyId));
    }

    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<Incident>>> GetIncidentsByEntity(string entityId)
    {
        return Ok(await _incidentService.GetIncidentsByEntityAsync(entityId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Incident>> GetIncident(string id)
    {
        var incident = await _incidentService.GetIncidentAsync(id);
        return incident == null ? NotFound() : Ok(incident);
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<Incident>> CreateIncident(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        Incident incident)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        incident.CompanyId = companyId;
        incident.CreatedBy = User?.Identity?.Name ?? "System";
        try
        {
            var created = await _incidentService.CreateIncidentAsync(incident);
            return CreatedAtAction(nameof(GetIncident), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error creating incident: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> UpdateIncident(string id, Incident incident)
    {
        if (id != incident.Id) return BadRequest("ID mismatch");
        incident.ModifiedBy = User?.Identity?.Name ?? "System";
        try
        {
            await _incidentService.UpdateIncidentAsync(incident);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error updating incident: {ex.Message}");
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<Incident>> UpdateIncidentStatus(
        string id, [FromBody] UpdateIncidentStatusRequest request)
    {
        try
        {
            var incident = await _incidentService.UpdateIncidentStatusAsync(
                id, request.Status, request.Message, User?.Identity?.Name ?? "System");
            return Ok(incident);
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest($"Error updating incident status: {ex.Message}"); }
    }

    [HttpPut("{id}/resolve")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<Incident>> ResolveIncident(
        string id, [FromBody] ResolveIncidentRequest request)
    {
        try
        {
            var incident = await _incidentService.ResolveIncidentAsync(
                id, request.ResolutionDetails, User?.Identity?.Name ?? "System");
            return Ok(incident);
        }
        catch (ArgumentException ex) { return NotFound(ex.Message); }
        catch (Exception ex) { return BadRequest($"Error resolving incident: {ex.Message}"); }
    }

    [HttpGet("{id}/updates")]
    public async Task<ActionResult<IEnumerable<IncidentUpdate>>> GetIncidentUpdates(string id)
    {
        return Ok(await _incidentService.GetIncidentUpdatesAsync(id));
    }

    [HttpPost("{id}/updates")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<IncidentUpdate>> CreateIncidentUpdate(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        string id,
        [FromBody] CreateIncidentUpdateRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var update = new IncidentUpdate
        {
            IncidentId = id,
            Message = request.Message,
            StatusChange = request.StatusChange,
            Author = User?.Identity?.Name ?? "System",
            CompanyId = companyId
        };
        try
        {
            return Ok(await _incidentService.CreateIncidentUpdateAsync(update));
        }
        catch (Exception ex) { return BadRequest($"Error creating incident update: {ex.Message}"); }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteIncident(string id)
    {
        try
        {
            await _incidentService.DeleteIncidentAsync(id);
            return NoContent();
        }
        catch (Exception ex) { return BadRequest($"Error deleting incident: {ex.Message}"); }
    }

    [HttpGet("stats/active-count")]
    public async Task<ActionResult<int>> GetActiveIncidentCount(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _incidentService.GetActiveIncidentCountAsync(companyId));
    }

    [HttpGet("stats/critical-count")]
    public async Task<ActionResult<int>> GetCriticalIncidentCount(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _incidentService.GetCriticalIncidentCountAsync(companyId));
    }
}

public class UpdateIncidentStatusRequest
{
    public IncidentStatus Status { get; set; }
    public string? Message { get; set; }
}

public class ResolveIncidentRequest
{
    public string ResolutionDetails { get; set; } = string.Empty;
}

public class CreateIncidentUpdateRequest
{
    public string Message { get; set; } = string.Empty;
    public IncidentStatus? StatusChange { get; set; }
}

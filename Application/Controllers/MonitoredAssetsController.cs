using Application.Shared.Models;
using Application.Shared.Services;
using Application.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/status/entities")]
[ApiController]
public class MonitoredAssetsController : ControllerBase
{
    private readonly IMonitoredAssetService _entityService;

    public MonitoredAssetsController(IMonitoredAssetService entityService)
    {
        _entityService = entityService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetEntitiesAsync(companyId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MonitoredAsset>> GetEntity(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        var entity = await _entityService.GetEntityAsync(id);
        return entity == null ? NotFound() : Ok(entity);
    }

    [HttpPost]
    public async Task<ActionResult<MonitoredAsset>> CreateEntity(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        MonitoredAsset entity)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        entity.CompanyId = companyId;
        entity.CreatedBy = User?.Identity?.Name ?? "System";
        var created = await _entityService.CreateEntityAsync(entity);
        return CreatedAtAction(nameof(GetEntity), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEntity(
        string id,
        MonitoredAsset entity,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        if (id != entity.Id) return BadRequest("Entity ID mismatch");
        if (await _entityService.GetEntityAsync(id) == null) return NotFound();
        entity.ModifiedBy = User?.Identity?.Name ?? "System";
        await _entityService.UpdateEntityAsync(entity);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEntity(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        return await _entityService.DeleteEntityAsync(id) ? NoContent() : NotFound();
    }

    [HttpGet("type/{entityType}")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntitiesByType(
        [FromHeader(Name = "X-Company-Id")] string companyId, AssetType entityType)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetEntitiesByTypeAsync(companyId, entityType));
    }

    [HttpGet("critical")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetCriticalEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetCriticalEntitiesAsync(companyId));
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetActiveEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetActiveEntitiesAsync(companyId));
    }

    [HttpGet("with-latest-status")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntitiesWithLatestStatus(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetEntitiesWithLatestStatusAsync(companyId));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<Dictionary<string, int>>> GetEntityStatusSummary(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        var summary = await _entityService.GetEntityStatusSummaryAsync(companyId);
        return Ok(summary.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> SearchEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] string searchTerm)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.SearchEntitiesAsync(companyId, searchTerm));
    }

    [HttpGet("{id}/status/latest")]
    public async Task<ActionResult<AssetStatusHistory>> GetLatestEntityStatus(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        var status = await _entityService.GetLatestEntityStatusAsync(id);
        return status == null ? NotFound() : Ok(status);
    }

    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateEntityStatus(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        string id,
        [FromBody] UpdateAssetStatusRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        try
        {
            var result = await _entityService.UpdateEntityStatusWithIncidentHandlingAsync(
                id, request.Status, request.StatusMessage, request.PreviousStatus,
                companyId, User?.Identity?.Name ?? "System");
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error updating entity status: {ex.Message}");
        }
    }

    [HttpPost("{id}/ping")]
    public async Task<ActionResult<bool>> PingEntity(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.PingEntityAsync(id));
    }

    [HttpGet("{id}/dependencies")]
    public async Task<ActionResult<IEnumerable<AssetDependency>>> GetEntityDependencies(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        return Ok(await _entityService.GetEntityDependenciesAsync(id));
    }

    [HttpGet("{id}/dependency-tree")]
    public async Task<ActionResult<AssetDependencyTree>> GetEntityDependencyTree(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_VIEW_STATUS")) return Forbid();
        try
        {
            return Ok(await _entityService.GetEntityDependencyTreeAsync(id));
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    [HttpPost("dependencies")]
    public async Task<ActionResult<AssetDependency>> CreateEntityDependency(
        AssetDependency dependency,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        var created = await _entityService.CreateEntityDependencyAsync(dependency);
        return CreatedAtAction(nameof(GetEntityDependencies), new { id = dependency.EntityId }, created);
    }

    [HttpPut("dependencies/{id}")]
    public async Task<IActionResult> UpdateEntityDependency(
        string id,
        AssetDependency dependency,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        if (id != dependency.Id) return BadRequest("Dependency ID mismatch");
        await _entityService.UpdateEntityDependencyAsync(dependency);
        return NoContent();
    }

    [HttpDelete("dependencies/{id}")]
    public async Task<IActionResult> DeleteEntityDependency(
        string id,
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (!User.IsInRole($"{companyId}_EDIT_STATUS")) return Forbid();
        return await _entityService.DeleteEntityDependencyAsync(id) ? NoContent() : NotFound();
    }
}

public class UpdateAssetStatusRequest
{
    public AssetStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public AssetStatus PreviousStatus { get; set; }
}

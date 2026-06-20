using Application.Shared.Authorization;
using Application.Shared.Models;
using Application.Shared.Services;
using Application.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

[Route("api/status/entities")]
[ApiController]
[Authorize(Policy = PolicyNames.StatusRead)]
public class MonitoredAssetsController : ControllerBase
{
    private readonly IMonitoredAssetService _entityService;
    private readonly IEntityAudienceService _audienceService;

    public MonitoredAssetsController(IMonitoredAssetService entityService, IEntityAudienceService audienceService)
    {
        _entityService = entityService;
        _audienceService = audienceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetEntitiesAsync(companyId));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MonitoredAsset>> GetEntity(string id)
    {
        var entity = await _entityService.GetEntityAsync(id);
        return entity == null ? NotFound() : Ok(entity);
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<MonitoredAsset>> CreateEntity(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        MonitoredAsset entity)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        entity.CompanyId = companyId;
        entity.CreatedBy = User?.Identity?.Name ?? "System";
        var created = await _entityService.CreateEntityAsync(entity);
        return CreatedAtAction(nameof(GetEntity), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> UpdateEntity(string id, MonitoredAsset entity)
    {
        if (id != entity.Id) return BadRequest("Entity ID mismatch");
        if (await _entityService.GetEntityAsync(id) == null) return NotFound();
        entity.ModifiedBy = User?.Identity?.Name ?? "System";
        await _entityService.UpdateEntityAsync(entity);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteEntity(string id)
    {
        return await _entityService.DeleteEntityAsync(id) ? NoContent() : NotFound();
    }

    [HttpGet("type/{entityType}")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntitiesByType(
        [FromHeader(Name = "X-Company-Id")] string companyId, AssetType entityType)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetEntitiesByTypeAsync(companyId, entityType));
    }

    [HttpGet("critical")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetCriticalEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetCriticalEntitiesAsync(companyId));
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetActiveEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetActiveEntitiesAsync(companyId));
    }

    [HttpGet("with-latest-status")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> GetEntitiesWithLatestStatus(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetEntitiesWithLatestStatusAsync(companyId));
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<MonitoredAsset>>> GetEntitiesPaged(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] EntityQueryParameters parameters)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetEntitiesPagedAsync(companyId, parameters));
    }

    [HttpGet("groups")]
    public async Task<ActionResult<IEnumerable<string>>> GetEntityGroups(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.GetEntityGroupsAsync(companyId));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<Dictionary<string, int>>> GetEntityStatusSummary(
        [FromHeader(Name = "X-Company-Id")] string companyId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var summary = await _entityService.GetEntityStatusSummaryAsync(companyId);
        return Ok(summary.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value));
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MonitoredAsset>>> SearchEntities(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        [FromQuery] string searchTerm)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _entityService.SearchEntitiesAsync(companyId, searchTerm));
    }

    [HttpGet("{id}/status/latest")]
    public async Task<ActionResult<AssetStatusHistory>> GetLatestEntityStatus(string id)
    {
        var status = await _entityService.GetLatestEntityStatusAsync(id);
        return status == null ? NotFound() : Ok(status);
    }

    [HttpPost("{id}/status")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> UpdateEntityStatus(
        [FromHeader(Name = "X-Company-Id")] string companyId,
        string id,
        [FromBody] UpdateAssetStatusRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
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
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<bool>> PingEntity(string id)
    {
        return Ok(await _entityService.PingEntityAsync(id));
    }

    [HttpGet("{id}/dependencies")]
    public async Task<ActionResult<IEnumerable<AssetDependency>>> GetEntityDependencies(string id)
    {
        return Ok(await _entityService.GetEntityDependenciesAsync(id));
    }

    [HttpGet("{id}/dependency-tree")]
    public async Task<ActionResult<AssetDependencyTree>> GetEntityDependencyTree(string id)
    {
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
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<AssetDependency>> CreateEntityDependency(AssetDependency dependency)
    {
        var created = await _entityService.CreateEntityDependencyAsync(dependency);
        return CreatedAtAction(nameof(GetEntityDependencies), new { id = dependency.EntityId }, created);
    }

    [HttpPut("dependencies/{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> UpdateEntityDependency(string id, AssetDependency dependency)
    {
        if (id != dependency.Id) return BadRequest("Dependency ID mismatch");
        await _entityService.UpdateEntityDependencyAsync(dependency);
        return NoContent();
    }

    [HttpDelete("dependencies/{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteEntityDependency(string id)
    {
        return await _entityService.DeleteEntityDependencyAsync(id) ? NoContent() : NotFound();
    }

    // ---- Audience (notification recipients per entity) ----

    [HttpGet("{id}/audience")]
    public async Task<ActionResult<IEnumerable<EntityAudience>>> GetEntityAudience(
        [FromHeader(Name = "X-Company-Id")] string companyId, string id)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _audienceService.GetAudienceForEntityAsync(id, companyId));
    }

    [HttpGet("{id}/audience/assignable-users")]
    public async Task<ActionResult<IEnumerable<object>>> GetAssignableAudienceUsers(
        [FromHeader(Name = "X-Company-Id")] string companyId, string id)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        var users = await _audienceService.GetAssignableUsersAsync(companyId);
        return Ok(users.Select(u => new { u.Id, u.Email, u.UserName }));
    }

    [HttpPost("{id}/audience")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<EntityAudience>> AddEntityAudience(
        [FromHeader(Name = "X-Company-Id")] string companyId, string id,
        [FromBody] AddAudienceRequest request)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (string.IsNullOrWhiteSpace(request?.ApplicationUserId)) return BadRequest("ApplicationUserId is required");
        try
        {
            var created = await _audienceService.AddAsync(id, request.ApplicationUserId, request.AudienceType, companyId);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("audience/{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<ActionResult<EntityAudience>> UpdateEntityAudience(
        [FromHeader(Name = "X-Company-Id")] string companyId, string id,
        [FromBody] EntityAudience audience)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        if (id != audience.Id) return BadRequest("Audience ID mismatch");
        audience.CompanyId = companyId;
        return Ok(await _audienceService.UpdateAsync(audience));
    }

    [HttpDelete("audience/{id}")]
    [Authorize(Policy = PolicyNames.StatusWrite)]
    public async Task<IActionResult> DeleteEntityAudience(
        [FromHeader(Name = "X-Company-Id")] string companyId, string id)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return await _audienceService.RemoveAsync(id, companyId) ? NoContent() : NotFound();
    }
}

public class UpdateAssetStatusRequest
{
    public AssetStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public AssetStatus PreviousStatus { get; set; }
}

public class AddAudienceRequest
{
    public string ApplicationUserId { get; set; } = string.Empty;
    public EntityAudienceType AudienceType { get; set; } = EntityAudienceType.Stakeholder;
}

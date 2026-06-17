using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Credential CRUD and service start/stop for Server-type entities. All endpoints require the
/// X-Company-Id header and verify the entity belongs to the company and is a Server.
/// </summary>
[Route("api/status/entities/{entityId}")]
[ApiController]
public class ServerManagementController : ControllerBase
{
    private readonly IServerCredentialService _credentialService;
    private readonly IServerManagementService _managementService;
    private readonly IMonitoredAssetService _entityService;

    public ServerManagementController(
        IServerCredentialService credentialService,
        IServerManagementService managementService,
        IMonitoredAssetService entityService)
    {
        _credentialService = credentialService;
        _managementService = managementService;
        _entityService = entityService;
    }

    // ---- Credentials (CRUD) ----

    [HttpGet("credentials")]
    public async Task<ActionResult<IEnumerable<ServerCredentialDto>>> GetCredentials(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        return Ok(await _credentialService.GetCredentialsAsync(entityId, companyId));
    }

    [HttpPost("credentials")]
    public async Task<ActionResult<ServerCredentialDto>> CreateCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, ServerCredentialRequest request)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        var created = await _credentialService.CreateAsync(entityId, companyId, request, User?.Identity?.Name ?? "System");
        return Ok(created);
    }

    [HttpPut("credentials/{credentialId}")]
    public async Task<ActionResult<ServerCredentialDto>> UpdateCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, string credentialId, ServerCredentialRequest request)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        var updated = await _credentialService.UpdateAsync(credentialId, entityId, companyId, request, User?.Identity?.Name ?? "System");
        return updated == null ? NotFound() : Ok(updated);
    }

    [HttpDelete("credentials/{credentialId}")]
    public async Task<IActionResult> DeleteCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, string credentialId)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        return await _credentialService.DeleteAsync(credentialId, entityId, companyId) ? NoContent() : NotFound();
    }

    // ---- Services (discover / start / stop) ----

    [HttpGet("services")]
    public async Task<ActionResult<IEnumerable<RemoteServiceInfo>>> DiscoverServices(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        [FromQuery] string? credentialId, CancellationToken ct)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        try
        {
            return Ok(await _managementService.DiscoverServicesAsync(entityId, credentialId, companyId, ct));
        }
        catch (Exception ex)
        {
            return BadRequest(ServiceActionResult.Fail(ex.Message));
        }
    }

    [HttpPost("services/{serviceName}/start")]
    public async Task<ActionResult<ServiceActionResult>> StartService(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, string serviceName,
        [FromQuery] string? credentialId, CancellationToken ct)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        try
        {
            return Ok(await _managementService.StartServiceAsync(entityId, credentialId, companyId, serviceName, ct));
        }
        catch (Exception ex)
        {
            return BadRequest(ServiceActionResult.Fail(ex.Message));
        }
    }

    [HttpPost("services/{serviceName}/stop")]
    public async Task<ActionResult<ServiceActionResult>> StopService(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, string serviceName,
        [FromQuery] string? credentialId, CancellationToken ct)
    {
        var guard = await ValidateServerAsync(companyId, entityId);
        if (guard != null) return guard;
        try
        {
            return Ok(await _managementService.StopServiceAsync(entityId, credentialId, companyId, serviceName, ct));
        }
        catch (Exception ex)
        {
            return BadRequest(ServiceActionResult.Fail(ex.Message));
        }
    }

    /// <summary>Returns an error result when the request is invalid, otherwise null to proceed.</summary>
    private async Task<ActionResult?> ValidateServerAsync(string companyId, string entityId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");

        var entity = await _entityService.GetEntityAsync(entityId);
        if (entity == null || entity.CompanyId != companyId) return NotFound();
        if (entity.EntityType != AssetType.Server)
            return BadRequest("Service management is only available for Server entities.");

        return null;
    }
}

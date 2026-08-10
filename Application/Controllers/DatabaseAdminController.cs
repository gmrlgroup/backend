using Application.Shared.Authorization;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Database Management: space usage and user provisioning for Database-type entities.
/// All endpoints require the X-Company-Id header and verify the entity belongs to the company
/// and is a Database.
/// <para>
/// Mutating endpoints additionally check the requested username against the database's actual user
/// list, so a drop or password reset can only ever target a principal the caller can already see.
/// </para>
/// </summary>
[Route("api/database-admin")]
[ApiController]
[Authorize(Policy = PolicyNames.DatabaseAdmin)]
public class DatabaseAdminController : ControllerBase
{
    private readonly IDatabaseAdminService _service;
    private readonly IMonitoredAssetService _entityService;

    public DatabaseAdminController(IDatabaseAdminService service, IMonitoredAssetService entityService)
    {
        _service = service;
        _entityService = entityService;
    }

    // ---- Databases ----

    [HttpGet("databases")]
    public async Task<ActionResult<IEnumerable<DatabaseAdminEntityDto>>> GetDatabases(
        [FromHeader(Name = "X-Company-Id")] string companyId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");
        return Ok(await _service.GetDatabaseEntitiesAsync(companyId, ct));
    }

    // ---- Admin credential ----

    [HttpGet("databases/{entityId}/credential")]
    public async Task<ActionResult<DatabaseAdminCredentialDto>> GetCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        var credential = await _service.GetAdminCredentialAsync(entityId, companyId, ct);
        return credential == null ? NoContent() : Ok(credential);
    }

    [HttpPut("databases/{entityId}/credential")]
    [Authorize(Policy = PolicyNames.DatabaseAdminWrite)]
    public async Task<ActionResult<DatabaseAdminCredentialDto>> SaveCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        DatabaseAdminCredentialRequest request, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("An admin username is required.");

        return Ok(await _service.SaveAdminCredentialAsync(entityId, companyId, request, User?.Identity?.Name ?? "System", ct));
    }

    [HttpDelete("databases/{entityId}/credential")]
    [Authorize(Policy = PolicyNames.DatabaseAdminWrite)]
    public async Task<IActionResult> DeleteCredential(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        return await _service.DeleteAdminCredentialAsync(entityId, companyId, ct) ? NoContent() : NotFound();
    }

    // ---- Size ----

    [HttpGet("databases/{entityId}/size")]
    public async Task<ActionResult<DatabaseSizeDto>> GetSize(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        return Ok(await _service.GetSizeAsync(entityId, companyId, ct));
    }

    // ---- Users ----

    [HttpGet("databases/{entityId}/users")]
    public async Task<ActionResult<DatabaseUserListDto>> GetUsers(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        return Ok(await _service.ListUsersAsync(entityId, companyId, ct));
    }

    [HttpPost("databases/{entityId}/users")]
    [Authorize(Policy = PolicyNames.DatabaseAdminWrite)]
    public async Task<ActionResult<DatabaseUserOperationResult>> CreateUser(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        CreateDatabaseUserRequest request, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        return Ok(await _service.CreateUserAsync(entityId, companyId, request, ct));
    }

    [HttpPost("databases/{entityId}/users/reset-password")]
    [Authorize(Policy = PolicyNames.DatabaseAdminWrite)]
    public async Task<ActionResult<DatabaseUserOperationResult>> ResetPassword(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        ResetDatabaseUserPasswordRequest request, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        var existing = await RequireExistingUserAsync(companyId, entityId, request.Username, ct);
        if (existing != null) return existing;

        return Ok(await _service.ResetPasswordAsync(entityId, companyId, request, ct));
    }

    [HttpDelete("databases/{entityId}/users")]
    [Authorize(Policy = PolicyNames.DatabaseAdminWrite)]
    public async Task<ActionResult<DatabaseUserOperationResult>> DropUser(
        [FromHeader(Name = "X-Company-Id")] string companyId, string entityId,
        [FromBody] DropDatabaseUserRequest request, CancellationToken ct)
    {
        var guard = await ValidateDatabaseAsync(companyId, entityId);
        if (guard != null) return guard;

        var existing = await RequireExistingUserAsync(companyId, entityId, request.Username, ct);
        if (existing != null) return existing;

        return Ok(await _service.DropUserAsync(entityId, companyId, request, ct));
    }

    // ---- Guards ----

    /// <summary>Returns an error result when the request is invalid, otherwise null to proceed.</summary>
    private async Task<ActionResult?> ValidateDatabaseAsync(string companyId, string entityId)
    {
        if (string.IsNullOrEmpty(companyId)) return BadRequest("X-Company-Id header is required");

        var entity = await _entityService.GetEntityAsync(entityId);
        if (entity == null || entity.CompanyId != companyId) return NotFound();
        if (entity.EntityType != AssetType.Database)
            return BadRequest("Database management is only available for Database entities.");

        return null;
    }

    /// <summary>
    /// Refuses a destructive operation unless the named principal is actually present on the database.
    /// This keeps drop/reset to targets the caller can enumerate, and turns a typo into a 400 rather than
    /// an opaque engine error.
    /// </summary>
    private async Task<ActionResult?> RequireExistingUserAsync(string companyId, string entityId, string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new DatabaseUserOperationResult { Ok = false, Error = "A username is required." });

        var users = await _service.ListUsersAsync(entityId, companyId, ct);
        if (users.Error != null)
            return BadRequest(new DatabaseUserOperationResult { Ok = false, Username = username, Error = users.Error });

        if (!users.Users.Any(u => string.Equals(u.Name, username, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new DatabaseUserOperationResult
            {
                Ok = false,
                Username = username,
                Error = $"'{username}' was not found on this database."
            });

        return null;
    }
}

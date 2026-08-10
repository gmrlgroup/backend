using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Application.Shared.Services;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// SQL query workbench for a dataset's DuckDB tables: ad-hoc execution, write-back (save result as
/// table/view), and saved-query CRUD.
/// </summary>
/// <remarks>
/// Every action requires the company's <c>QUERY</c> role (plus the <c>QueryAccess</c> policy). Within
/// <see cref="Run"/>, a data-modifying statement additionally requires <c>DATA_ADMIN</c>.
/// <para>
/// The previous summary here claimed "Read execution is open to VIEW_DATA; write/DDL needs EDIT_DATA".
/// Neither role exists in <see cref="RoleSuffixes"/> — the summary described a model that was never
/// implemented, which is how the <c>allowWrite</c> defect below went unnoticed. <see cref="SaveResult"/>
/// still performs write-back (it materializes a table or view) behind <c>QUERY</c> alone; that is
/// long-standing behaviour rather than a contradiction, but it is inconsistent with
/// <see cref="Run"/> and worth a deliberate decision.
/// </para>
/// </remarks>
[Route("api/datasets/{datasetId}")]
[ApiController]
[Authorize(Policy = PolicyNames.QueryAccess)]
public class QueryController : ControllerBase
{
    private readonly IDuckdbService _duckdbService;
    private readonly IDatasetService _datasetService;
    private readonly ISavedQueryService _savedQueryService;
    private readonly IDatabaseTableService _databaseTableService;
    private readonly IIngestionService _ingestionService;
    private readonly ISqlTableResolver _tableResolver;

    public QueryController(
        IDuckdbService duckdbService,
        IDatasetService datasetService,
        ISavedQueryService savedQueryService,
        IDatabaseTableService databaseTableService,
        IIngestionService ingestionService,
        ISqlTableResolver tableResolver)
    {
        _duckdbService = duckdbService;
        _datasetService = datasetService;
        _savedQueryService = savedQueryService;
        _databaseTableService = databaseTableService;
        _ingestionService = ingestionService;
        _tableResolver = tableResolver;
    }

    // POST: api/datasets/{datasetId}/query/run
    [HttpPost("query/run")]
    public async Task<ActionResult<SqlQueryResult>> Run(string datasetId, [FromBody] SqlQueryRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();
        if (string.IsNullOrWhiteSpace(request?.Sql)) return BadRequest("Query is required");

        var dataset = await _datasetService.GetDatasetAsync(datasetId, userId);
        if (dataset == null)
            return NotFound($"Dataset '{datasetId}' not found.");

        // Table-level share scope: block queries that reference a dataset table the user can't access.
        var blockedTable = await FindDisallowedTableAsync(dataset, userId, companyId, request.Sql, request.SnapshotMode, HttpContext.RequestAborted);
        if (blockedTable != null)
            return Ok(new SqlQueryResult { IsSelect = true, Error = $"Access to table '{blockedTable}' is not permitted." });

        // External datasets run against the live source connection (always read-only). The
        // 'snapshot' query param lets the workbench query the local DuckDB snapshots instead.
        if (dataset.SourceType == DatasetSourceType.External && !request.SnapshotMode)
        {
            var external = await _databaseTableService.ExecuteQueryAsync(
                dataset.SourceEntityId ?? "", companyId, request.Sql, request.MaxRows ?? 0, HttpContext.RequestAborted);
            return Ok(external);
        }

        // Writes/DDL require DATA_ADMIN (or {companyId}_ADMIN, which HasCompanyRole passes implicitly).
        // The service classifies the statement and returns a clear error inline if a write is attempted
        // without edit rights.
        //
        // This previously read HasCompanyRole(companyId, "QUERY") — the same predicate the guard at the
        // top of this method already requires — so allowWrite was ALWAYS true for anyone who got here.
        // QUERY is a module-visibility role, so it was enough to run DELETE/DROP against any dataset the
        // user could open, contradicting both this comment and the class summary.
        //
        // DATA_ADMIN is the closest real role: RoleSuffixes has no EDIT_DATA or VIEW_DATA at all, so the
        // roles the old comment named do not exist. Which role should authorize a write is a product
        // decision — if plain QUERY users are expected to keep write-back through this endpoint, widen
        // this line deliberately rather than by reverting it.
        var hasEdit = User.HasCompanyRole(companyId, RoleSuffixes.DataAdmin);
        var result = await _duckdbService.ExecuteSqlAsync(
            datasetId, request.Sql, allowWrite: hasEdit, maxRows: request.MaxRows ?? 0, HttpContext.RequestAborted);
        return Ok(result);
    }

    // POST: api/datasets/{datasetId}/query/save-result  — write-back as a new table or view.
    [HttpPost("query/save-result")]
    public async Task<ActionResult<SqlQueryResult>> SaveResult(string datasetId, [FromBody] SaveResultRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Sql) || string.IsNullOrWhiteSpace(request.ObjectName))
            return BadRequest("Query and object name are required");

        var dataset = await _datasetService.GetDatasetAsync(datasetId, userId);
        if (dataset == null)
            return NotFound($"Dataset '{datasetId}' not found.");

        // Table-level share scope: the SELECT being materialized must not reference disallowed tables.
        var blockedTable = await FindDisallowedTableAsync(dataset, userId, companyId, request.Sql, request.SnapshotMode, HttpContext.RequestAborted);
        if (blockedTable != null)
            return Ok(new SqlQueryResult { Error = $"Access to table '{blockedTable}' is not permitted." });

        // External source mode: there is no write-back to a read-only source, so the result is snapshotted
        // into a local DuckDB table instead. (Snapshot mode / Local datasets fall through to DuckDB write-back.)
        if (dataset.SourceType == DatasetSourceType.External && !request.SnapshotMode)
        {
            var import = await _ingestionService.SnapshotQueryAsync(
                companyId, datasetId, dataset.SourceEntityId ?? "", request.Sql, request.ObjectName, HttpContext.RequestAborted);
            return Ok(new SqlQueryResult
            {
                IsSelect = false,
                Error = import.Error,
                RowsAffected = import.RowsInserted + import.RowsUpdated
            });
        }

        var result = await _duckdbService.CreateObjectFromQueryAsync(
            datasetId, request.ObjectName, request.Sql, request.AsView, HttpContext.RequestAborted);
        return Ok(result);
    }

    // GET: api/datasets/{datasetId}/queries
    [HttpGet("/api/datasets/{datasetId}/queries")]
    public async Task<ActionResult<IEnumerable<SavedQueryDto>>> GetSavedQueries(string datasetId)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();

        return Ok(await _savedQueryService.GetForDatasetAsync(companyId, datasetId, userId));
    }

    // POST: api/datasets/{datasetId}/queries
    [HttpPost("/api/datasets/{datasetId}/queries")]
    public async Task<ActionResult<SavedQueryDto>> CreateSavedQuery(string datasetId, [FromBody] SaveSavedQueryRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.QueryText))
            return BadRequest("Name and query text are required");
        if (!await DatasetAccessible(datasetId, userId))
            return NotFound($"Dataset '{datasetId}' not found.");

        return Ok(await _savedQueryService.CreateAsync(companyId, datasetId, userId, request));
    }

    // PUT: api/datasets/{datasetId}/queries/{id}
    [HttpPut("/api/datasets/{datasetId}/queries/{id}")]
    public async Task<ActionResult<SavedQueryDto>> UpdateSavedQuery(string datasetId, string id, [FromBody] SaveSavedQueryRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.QueryText))
            return BadRequest("Name and query text are required");

        var isAdmin = User.HasCompanyRole(companyId, RoleSuffixes.Admin);
        var updated = await _savedQueryService.UpdateAsync(companyId, id, userId, isAdmin, request);
        if (updated == null) return NotFound("Query not found, or you don't have permission to edit it.");
        return Ok(updated);
    }

    // DELETE: api/datasets/{datasetId}/queries/{id}
    [HttpDelete("/api/datasets/{datasetId}/queries/{id}")]
    public async Task<IActionResult> DeleteSavedQuery(string datasetId, string id)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, RoleSuffixes.Query)) return Forbid();

        var isAdmin = User.HasCompanyRole(companyId, RoleSuffixes.Admin);
        if (!await _savedQueryService.DeleteAsync(companyId, id, userId, isAdmin))
            return NotFound("Query not found, or you don't have permission to delete it.");
        return NoContent();
    }

    /// <summary>
    /// Best-effort table-level guard for ad-hoc SQL: returns the name of a referenced table the user is
    /// NOT allowed to access, or null if the query is clear. Only blocks references that match a KNOWN
    /// dataset table outside the user's allow-list, so CTEs/aliases/functions are never false-flagged.
    /// </summary>
    /// <remarks>
    /// The extraction itself now lives in <see cref="ISqlTableResolver"/>, shared with the public query
    /// endpoint, so there is one implementation of "what tables does this query touch". This method keeps
    /// the workbench's permissive policy — see <see cref="SqlTableResolution.FirstDisallowedKnownTable"/>.
    /// </remarks>
    private async Task<string?> FindDisallowedTableAsync(Dataset dataset, string userId, string companyId, string sql, bool snapshotMode, System.Threading.CancellationToken ct)
    {
        var resolution = await _tableResolver.ResolveAsync(dataset, userId, companyId, sql, snapshotMode, ct);
        return resolution.FirstDisallowedKnownTable;
    }

    private (string companyId, string userId, string? error) ReadHeaders()
    {
        var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "";
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(companyId)) return ("", "", "Company ID is required");
        if (string.IsNullOrWhiteSpace(userId)) return ("", "", "User ID is required in headers");
        return (companyId, userId, null);
    }

    private async Task<bool> DatasetAccessible(string datasetId, string userId)
        => !string.IsNullOrWhiteSpace(datasetId) && await _datasetService.GetDatasetAsync(datasetId, userId) != null;
}

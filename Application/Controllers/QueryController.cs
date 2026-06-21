using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// SQL query workbench for a dataset's DuckDB tables: ad-hoc execution, write-back (save result as
/// table/view), and saved-query CRUD. Read execution is open to VIEW_DATA; write/DDL needs EDIT_DATA.
/// </summary>
[Route("api/datasets/{datasetId}")]
[ApiController]
[Authorize(Policy = PolicyNames.DatasetsAccess)]
public class QueryController : ControllerBase
{
    private readonly IDuckdbService _duckdbService;
    private readonly IDatasetService _datasetService;
    private readonly ISavedQueryService _savedQueryService;

    public QueryController(IDuckdbService duckdbService, IDatasetService datasetService, ISavedQueryService savedQueryService)
    {
        _duckdbService = duckdbService;
        _datasetService = datasetService;
        _savedQueryService = savedQueryService;
    }

    // POST: api/datasets/{datasetId}/query/run
    [HttpPost("query/run")]
    public async Task<ActionResult<SqlQueryResult>> Run(string datasetId, [FromBody] SqlQueryRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();
        if (string.IsNullOrWhiteSpace(request?.Sql)) return BadRequest("Query is required");
        if (!await DatasetAccessible(datasetId, userId))
            return NotFound($"Dataset '{datasetId}' not found.");

        // VIEW_DATA → read-only; EDIT_DATA/ADMIN → writes allowed. The service classifies the
        // statement and returns a clear error inline if a write is attempted without edit rights.
        var hasEdit = User.HasCompanyRole(companyId, "EDIT_DATA");
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
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Sql) || string.IsNullOrWhiteSpace(request.ObjectName))
            return BadRequest("Query and object name are required");
        if (!await DatasetAccessible(datasetId, userId))
            return NotFound($"Dataset '{datasetId}' not found.");

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
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();

        return Ok(await _savedQueryService.GetForDatasetAsync(companyId, datasetId, userId));
    }

    // POST: api/datasets/{datasetId}/queries
    [HttpPost("/api/datasets/{datasetId}/queries")]
    public async Task<ActionResult<SavedQueryDto>> CreateSavedQuery(string datasetId, [FromBody] SaveSavedQueryRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();
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
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.QueryText))
            return BadRequest("Name and query text are required");

        var isAdmin = User.HasCompanyRole(companyId, "ADMIN");
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
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();

        var isAdmin = User.HasCompanyRole(companyId, "ADMIN");
        if (!await _savedQueryService.DeleteAsync(companyId, id, userId, isAdmin))
            return NotFound("Query not found, or you don't have permission to delete it.");
        return NoContent();
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

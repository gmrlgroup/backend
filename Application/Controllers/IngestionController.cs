using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// Manages a dataset's scheduled ingestion sources (external DB / REST / Blob / SFTP pulls) and their
/// run history. Mutations require EDIT_DATA; secrets are encrypted on write and never returned.
/// </summary>
[Route("api/datasets/{datasetId}/ingestion")]
[ApiController]
[Authorize(Policy = PolicyNames.DatasetsAccess)]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestionService;
    private readonly IDatasetService _datasetService;

    public IngestionController(IIngestionService ingestionService, IDatasetService datasetService)
    {
        _ingestionService = ingestionService;
        _datasetService = datasetService;
    }

    // GET: api/datasets/{datasetId}/ingestion
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IngestionSourceDto>>> GetSources(string datasetId)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();
        if (!await DatasetAccessible(datasetId, userId)) return NotFound($"Dataset '{datasetId}' not found.");

        return Ok(await _ingestionService.GetSourcesAsync(companyId, datasetId, HttpContext.RequestAborted));
    }

    // GET: api/datasets/{datasetId}/ingestion/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<IngestionSourceDto>> GetSource(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();

        var source = await _ingestionService.GetSourceAsync(companyId, id, HttpContext.RequestAborted);
        return source == null ? NotFound() : Ok(source);
    }

    // POST: api/datasets/{datasetId}/ingestion
    [HttpPost]
    public async Task<ActionResult<IngestionSourceDto>> CreateSource(string datasetId, [FromBody] SaveIngestionSourceRequest request)
    {
        var (companyId, userId, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TargetTable))
            return BadRequest("Name and target table are required");
        if (!await DatasetAccessible(datasetId, userId)) return NotFound($"Dataset '{datasetId}' not found.");

        return Ok(await _ingestionService.CreateAsync(companyId, datasetId, userId, request, HttpContext.RequestAborted));
    }

    // PUT: api/datasets/{datasetId}/ingestion/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<IngestionSourceDto>> UpdateSource(string datasetId, string id, [FromBody] SaveIngestionSourceRequest request)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.TargetTable))
            return BadRequest("Name and target table are required");

        var updated = await _ingestionService.UpdateAsync(companyId, id, request, HttpContext.RequestAborted);
        return updated == null ? NotFound() : Ok(updated);
    }

    // DELETE: api/datasets/{datasetId}/ingestion/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSource(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();

        return await _ingestionService.DeleteAsync(companyId, id, HttpContext.RequestAborted)
            ? NoContent()
            : NotFound();
    }

    // GET: api/datasets/{datasetId}/ingestion/{id}/runs
    [HttpGet("{id}/runs")]
    public async Task<ActionResult<IEnumerable<IngestionRunDto>>> GetRuns(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "VIEW_DATA")) return Forbid();

        return Ok(await _ingestionService.GetRunsAsync(companyId, id, 20, HttpContext.RequestAborted));
    }

    // POST: api/datasets/{datasetId}/ingestion/{id}/run  — "Run now" (executes inline).
    [HttpPost("{id}/run")]
    public async Task<ActionResult<ImportResult>> RunNow(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();

        // Confirm the source belongs to this company before running it.
        var source = await _ingestionService.GetSourceAsync(companyId, id, HttpContext.RequestAborted);
        if (source == null) return NotFound();

        var result = await _ingestionService.RunSourceAsync(id, HttpContext.RequestAborted);
        return Ok(result);
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

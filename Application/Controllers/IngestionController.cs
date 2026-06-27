using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public IngestionController(IIngestionService ingestionService, IDatasetService datasetService, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _ingestionService = ingestionService;
        _datasetService = datasetService;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
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

        var runs = await _ingestionService.GetRunsAsync(companyId, id, 20, HttpContext.RequestAborted);

        // Decorate Hangfire-executed runs with a dashboard deep-link when a dashboard URL is configured.
        var dashboardUrl = _configuration["Hangfire:DashboardUrl"];
        if (!string.IsNullOrWhiteSpace(dashboardUrl))
        {
            var baseUrl = dashboardUrl.TrimEnd('/');
            foreach (var run in runs)
                if (!string.IsNullOrEmpty(run.JobId))
                    run.JobUrl = $"{baseUrl}/jobs/details/{run.JobId}";
        }

        return Ok(runs);
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

        var result = await _ingestionService.RunSourceAsync(id, ct: HttpContext.RequestAborted);
        return Ok(result);
    }

    // POST: api/datasets/{datasetId}/ingestion/{id}/run-batch  — enqueue a Hangfire background job.
    // The job runs in the Application.Scheduler process (not inline in this request), so it survives the
    // web request ending and won't tie up a request thread for long pulls.
    [HttpPost("{id}/run-batch")]
    public async Task<ActionResult> RunBatch(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();

        // Confirm the source belongs to this company before queueing it.
        var source = await _ingestionService.GetSourceAsync(companyId, id, HttpContext.RequestAborted);
        if (source == null) return NotFound();

        // The Hangfire client is only registered when the SchedulerDbContext connection string is present.
        var jobClient = _serviceProvider.GetService<IBackgroundJobClient>();
        if (jobClient == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "Batch execution isn't configured. Add the 'SchedulerDbContext' connection string to the web app so jobs can be queued for the scheduler.");

        // Create the run row up front (Queued) so it exists before the worker starts and shows immediately
        // in the UI. The worker reuses this exact run via its id; we then attach the Hangfire job id to it.
        var runId = await _ingestionService.CreateQueuedRunAsync(companyId, id, HttpContext.RequestAborted);

        // Enqueue the shared IngestionJob wrapper (not RunSourceAsync directly) so the worker runs with a
        // Hangfire PerformContext — that's what enables dashboard console logs + the progress bar. The
        // scheduler resolves IngestionJob and runs the same RunSourceAsync, transitioning this Queued run.
        var jobId = jobClient.Enqueue<IngestionJob>(job => job.RunAsync(id, runId, null, CancellationToken.None));
        await _ingestionService.SetRunJobIdAsync(runId, jobId, HttpContext.RequestAborted);

        return Ok(new { jobId, runId });
    }

    // POST: api/datasets/{datasetId}/ingestion/{id}/runs/reconcile
    // Marks this source's stuck "Running" runs as Failed (e.g. a "Run now" orphaned by an app restart).
    [HttpPost("{id}/runs/reconcile")]
    public async Task<ActionResult> ReconcileRuns(string datasetId, string id)
    {
        var (companyId, _, error) = ReadHeaders();
        if (error != null) return BadRequest(error);
        if (!User.HasCompanyRole(companyId, "EDIT_DATA")) return Forbid();

        // Confirm the source belongs to this company before touching its runs.
        var source = await _ingestionService.GetSourceAsync(companyId, id, HttpContext.RequestAborted);
        if (source == null) return NotFound();

        var cleared = await _ingestionService.FailRunningRunsForSourceAsync(companyId, id, HttpContext.RequestAborted);
        return Ok(new { cleared });
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

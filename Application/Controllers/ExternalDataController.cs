using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Authorization;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers;

/// <summary>
/// External, API-key authenticated data access. Authentication comes solely from the API key
/// (no cookie/OIDC, no company header) — the key carries its own company, and every action is
/// gated by the key's per-dataset/table read or import grant.
/// </summary>
[Route("api/external")]
[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]
public class ExternalDataController : ControllerBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly IDuckdbService _duckdbService;

    public ExternalDataController(IApiKeyService apiKeyService, IDuckdbService duckdbService)
    {
        _apiKeyService = apiKeyService;
        _duckdbService = duckdbService;
    }

    // GET: api/external/datasets/{datasetId}/tables
    // Lists only the tables the key is allowed to read.
    [HttpGet("datasets/{datasetId}/tables")]
    public async Task<ActionResult<IEnumerable<string>>> GetTables(string datasetId)
    {
        var key = CurrentKey;
        if (key == null) return Unauthorized();

        // Caller must have at least one read grant somewhere in this dataset.
        if (!_apiKeyService.IsInScope(key, datasetId, null, ApiKeyOperation.Read))
            return Forbid();

        var tables = (await _duckdbService.GetTablesAsync(datasetId) ?? Enumerable.Empty<string>())
            .Where(t => _apiKeyService.IsInScope(key, datasetId, t, ApiKeyOperation.Read))
            .ToList();

        return Ok(tables);
    }

    // GET: api/external/datasets/{datasetId}/tables/{tableName}/columns
    [HttpGet("datasets/{datasetId}/tables/{tableName}/columns")]
    public async Task<ActionResult<IEnumerable<Column>>> GetColumns(string datasetId, string tableName)
    {
        var denied = CheckScope(datasetId, tableName, ApiKeyOperation.Read);
        if (denied != null) return denied;

        try
        {
            return await _duckdbService.GetTableColumnsAsync(datasetId, tableName);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error retrieving columns: {ex.Message}");
        }
    }

    // POST: api/external/datasets/{datasetId}/tables/{tableName}/data
    // Paged + filtered row query (same engine the UI uses).
    [HttpPost("datasets/{datasetId}/tables/{tableName}/data")]
    public async Task<ActionResult<TableDataResult>> GetData(string datasetId, string tableName, [FromBody] TableDataQuery? query)
    {
        var denied = CheckScope(datasetId, tableName, ApiKeyOperation.Read);
        if (denied != null) return denied;

        try
        {
            query ??= new TableDataQuery();
            query.DatasetId = datasetId;
            query.TableName = tableName;
            if (query.Page <= 0) query.Page = 1;
            if (query.PageSize <= 0) query.PageSize = 100;

            var result = await _duckdbService.QueryTableDataAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error querying table data: {ex.Message}");
        }
    }

    // GET: api/external/datasets/{datasetId}/tables/{tableName}/download
    [HttpGet("datasets/{datasetId}/tables/{tableName}/download")]
    public async Task<ActionResult> Download(string datasetId, string tableName)
    {
        var denied = CheckScope(datasetId, tableName, ApiKeyOperation.Read);
        if (denied != null) return denied;

        try
        {
            var query = new TableDataQuery
            {
                DatasetId = datasetId,
                TableName = tableName,
                Page = 1,
                PageSize = int.MaxValue, // full table for download
            };

            var data = await _duckdbService.QueryTableDataAsync(query);
            if (data?.Data == null || !data.Data.Any())
                return NotFound($"No data found for table '{tableName}'.");

            var csv = new StringBuilder();
            List<string> headers;
            if (data.Columns?.Any() == true)
                headers = data.Columns.Select(c => c.Name).ToList();
            else
                headers = data.Data.First().Keys.ToList();

            csv.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));
            foreach (var row in data.Data)
            {
                var values = headers.Select(h =>
                {
                    var v = row.TryGetValue(h, out var cell) ? cell : null;
                    return $"\"{v?.ToString()?.Replace("\"", "\"\"") ?? ""}\"";
                });
                csv.AppendLine(string.Join(",", values));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"{tableName}.csv");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error downloading table data: {ex.Message}");
        }
    }

    // POST: api/external/datasets/{datasetId}/tables/{tableName}/import
    // Appends CSV rows into an existing table. Requires an import grant for the table.
    [HttpPost("datasets/{datasetId}/tables/{tableName}/import")]
    [RequestSizeLimit(300_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]
    public async Task<ActionResult> Import(string datasetId, string tableName)
    {
        var denied = CheckScope(datasetId, tableName, ApiKeyOperation.Import);
        if (denied != null) return denied;

        try
        {
            if (!Request.HasFormContentType)
                return BadRequest("Request must be multipart/form-data with a CSV file field named 'file'.");

            var form = await Request.ReadFormAsync();
            if (form.Files.Count == 0)
                return BadRequest("CSV file is required");

            var csvFile = form.Files[0];
            if (csvFile.Length == 0)
                return BadRequest("CSV file cannot be empty");

            if (!csvFile.ContentType.StartsWith("text/csv") &&
                !Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be a CSV file");

            using var stream = csvFile.OpenReadStream();
            var ok = await _duckdbService.ImportCsvDataAsync(datasetId, tableName, stream);
            if (!ok) return StatusCode(500, "Failed to import CSV data");

            return Ok(new { message = "CSV data imported successfully", datasetId, tableName });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error importing CSV data: {ex.Message}");
        }
    }

    // ---- scope plumbing ------------------------------------------------------------------------

    private ApiKey? CurrentKey =>
        HttpContext.Items.TryGetValue(ApiKeyAuthenticationDefaults.ApiKeyItem, out var v) ? v as ApiKey : null;

    /// <summary>Returns a 401/403 result when the current key may not perform the operation; otherwise null.</summary>
    private ActionResult? CheckScope(string datasetId, string? tableName, ApiKeyOperation operation)
    {
        var key = CurrentKey;
        if (key == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(datasetId)) return BadRequest("Dataset ID is required");
        if (!_apiKeyService.IsInScope(key, datasetId, tableName, operation)) return Forbid();
        return null;
    }
}

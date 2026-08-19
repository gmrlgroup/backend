using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Models.Logging;
using Application.Shared.Services.Data;
using Application.Shared.Services.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Application.Controllers;

/// <summary>
/// Read-only SQL execution for the API-key public API: <c>POST api/dataset/{datasetId}/query/run</c>.
/// Built for Relay's dataset agent, which has a language model write the SQL and needs it run as a
/// specific end user with that user's grants applied.
/// </summary>
/// <remarks>
/// Note the route prefix: the public API is <c>api/dataset</c> (singular) — the convention
/// <see cref="PublicDatasetController"/> established — while the internal cookie-authenticated workbench
/// is <c>api/datasets</c> (plural) on <see cref="QueryController"/>. The near-collision is deliberate but
/// reads like a typo, so it is called out here.
/// <para>
/// Authorization needs <b>both</b> the API key's dataset scope and the acting user's grants, unlike every
/// other endpoint on this surface. They answer different questions: the key scope is an operator's
/// control over the integration ("may this app touch this dataset at all" — pull the scope row and it
/// stops, whatever anyone's grants say), while the user grants decide which rows and columns. The key
/// scope matters especially here because <c>X-User-Id</c> is <b>caller-asserted</b> — whoever holds the
/// key can name any user in the company — so it is the only control bounding the damage if the calling
/// app is compromised. Existing endpoints on this surface return read-only metadata; this one executes
/// SQL, so it takes the stronger posture rather than the weaker.
/// </para>
/// </remarks>
[Route("api/dataset/{datasetId}/query")]
[EnableRateLimiting(PublicQueryController.RateLimitPolicy)]
[RequestSizeLimit(64_000)]
public class PublicQueryController : PublicApiControllerBase
{
    public const string RateLimitPolicy = "public-sql";

    private readonly IPublicSqlQueryService _query;
    private readonly IApiKeyService _apiKeys;
    private readonly IPublicApiUserAuthorizationService _userAuth;
    private readonly IDataAppLogService _log;
    private readonly IDebugLogService _debug;
    private readonly PublicApiOptions _options;

    public PublicQueryController(
        IPublicSqlQueryService query,
        IApiKeyService apiKeys,
        IPublicApiUserAuthorizationService userAuth,
        IDataAppLogService log,
        IDebugLogService debug,
        PublicApiOptions options)
    {
        _query = query;
        _apiKeys = apiKeys;
        _userAuth = userAuth;
        _log = log;
        _debug = debug;
        _options = options;
    }

    /// <summary>Runs one read-only SQL statement against the dataset as the acting user.</summary>
    /// <remarks>
    /// A SQL or permission problem <i>inside</i> the dataset comes back as 200 with
    /// <see cref="PublicSqlQueryResponse.ErrorCode"/> set — the same convention
    /// <see cref="SqlQueryResult"/> has always used, and what a model-driven caller needs so it can feed
    /// the reason back and retry. 403 is reserved for exactly one thing: the API key is not scoped to
    /// this dataset, which is an operator problem rather than a data-grant problem. Collapsing the two
    /// would make them indistinguishable in logs.
    /// </remarks>
    [HttpPost("run")]
    public async Task<ActionResult<PublicSqlQueryResponse>> Run(
        string datasetId, [FromBody] PublicSqlQueryRequest request, CancellationToken ct)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error)) return error!;

        var key = CurrentKey;
        if (key is null) return Unauthorized("Invalid or missing API key.");

        // Pass datasetId through verbatim: IsInScope compares DatasetId with StringComparison.Ordinal
        // (case-sensitive) while TableName is case-insensitive, so normalising case here would silently
        // fail the scope check.
        if (!_apiKeys.IsInScope(key, datasetId, null, ApiKeyOperation.Read))
            return StatusCode(StatusCodes.Status403Forbidden,
                $"This API key is not scoped to read dataset '{datasetId}'.");

        if (_options.EnforceActingUserRoles)
        {
            var auth = await _userAuth.AuthorizeAsync(companyId, userId,
                new[] { RoleSuffixes.Query, RoleSuffixes.DataAdmin }, ct);
            if (!auth.Allowed)
                return Ok(new PublicSqlQueryResponse
                {
                    Sql = request?.Sql ?? string.Empty,
                    ErrorCode = auth.ErrorCode,
                    Error = auth.Message
                });
        }

        // The wall-clock budget. This is the ONLY server-side bound on the external path:
        // DatabaseTableService.ExecuteQueryAsync has no CancelAfter of its own, so without this the query
        // runs until the client disconnects. It also shortens DuckDB's own 60s ceiling.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.EffectiveTimeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        PublicSqlQueryResponse? response = null;
        var statusCode = StatusCodes.Status200OK;

        try
        {
            response = await _query.RunAsync(companyId, userId, datasetId, request!,
                table => _apiKeys.IsInScope(key, datasetId, table, ApiKeyOperation.Read),
                cts.Token);

            if (response is null)
            {
                statusCode = StatusCodes.Status404NotFound;
                return NotFound($"Dataset '{datasetId}' not found.");
            }

            return Ok(response);
        }
        finally
        {
            stopwatch.Stop();
            SafeAudit(companyId, userId, datasetId, key, request, response, statusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Writes one audit row per call. Never allowed to affect the request.
    /// </summary>
    /// <remarks>
    /// Logged explicitly here rather than by adding this controller to
    /// <c>DataActivityLogFilter.TargetControllers</c>, for two reasons. That filter reads the company from
    /// <c>X-Company-ID</c> and the user from a <c>UserId</c> header, and the public API sends neither — it
    /// would record a blank tenant and user, which is worse than no row. And an action filter cannot see
    /// the response body, where the row count, the effective SQL, the referenced tables and the applied
    /// security live, which is the entire reason for auditing this endpoint.
    /// </remarks>
    private void SafeAudit(string companyId, string userId, string datasetId, ApiKey key,
        PublicSqlQueryRequest? request, PublicSqlQueryResponse? response, int statusCode, long elapsedMs)
    {
        try
        {
            if (!_log.Enabled) return;

            var sql = request?.Sql ?? string.Empty;
            if (sql.Length > 16_000) sql = sql[..16_000];

            var succeeded = statusCode == StatusCodes.Status200OK && response?.ErrorCode is null;

            _log.Enqueue(new DataAppLogEntry
            {
                EventTime = DateTime.UtcNow,
                // From the key, never a header: the header is caller-asserted, the key is authenticated.
                CompanyId = companyId,
                UserId = userId,
                // The integration, not the human — the acting user is UserId above.
                UserName = key.Name ?? string.Empty,
                Source = "public-api",
                Area = "table",
                Action = "public.query_run",
                DatasetId = datasetId,
                TableName = Truncate(string.Join(",", response?.TablesReferenced ?? new List<string>()), 500),
                HttpMethod = Request.Method,
                Route = Request.Path.Value ?? string.Empty,
                QueryString = Request.QueryString.Value ?? string.Empty,
                // The SQL text IS the audit record here, so it is stored even though it may contain
                // literals. DataActivityLogFilter's redaction does not run on this path, so this is a
                // deliberate choice rather than an oversight.
                QueryText = sql,
                RowCount = response?.RowsReturned ?? 0,
                StatusCode = statusCode,
                Success = succeeded,
                Error = response?.Error ?? string.Empty,
                DurationMs = elapsedMs,
                ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = Request.Headers.UserAgent.ToString(),
                Details = JsonSerializer.Serialize(new
                {
                    // The key id and prefix only. Never the key itself.
                    apiKeyId = key.Id,
                    apiKeyPrefix = key.KeyPrefix,
                    effectiveSql = Truncate(response?.EffectiveSql ?? string.Empty, 16_000),
                    rowCap = response?.RowCap ?? 0,
                    truncated = response?.Truncated ?? false,
                    snapshotMode = response?.SnapshotMode ?? false,
                    maskedColumns = response?.Security.MaskedColumns ?? new List<string>(),
                    rlsColumns = response?.Security.RowFilters.Select(f => $"{f.TableName}.{f.ColumnName}").ToList()
                                 ?? new List<string>(),
                    errorCode = response?.ErrorCode
                })
            });

            // Fail-closed outcomes are what an operator needs to fix a mis-seeded grant, and they are
            // invisible in a Success=false row alone.
            if (response?.ErrorCode is PublicSqlErrorCodes.SecurityNotEnforceable
                or PublicSqlErrorCodes.SchemaUnavailable
                or PublicSqlErrorCodes.CteNameConflict)
            {
                _ = _debug.LogAsync(companyId, "Warn", "public-sql",
                    $"Query refused ({response.ErrorCode}): {response.Error}",
                    datasetId: datasetId,
                    tableName: string.Join(",", response.TablesReferenced),
                    userId: userId);
            }
        }
        catch
        {
            // Auditing must never break or slow the request.
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

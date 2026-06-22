using System.Net.Http.Headers;
using System.Text.Json;
using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Services;

public class PowerBiService : IPowerBiService
{
    /// <summary>Named HttpClient used for both AAD token and Power BI REST calls (absolute URLs).</summary>
    public const string HttpClientName = "PowerBiApi";

    private const string PowerBiApiBase = "https://api.powerbi.com/v1.0/myorg";
    private const string PowerBiScope = "https://analysis.windows.net/powerbi/api/.default";

    private readonly StatusDbContext _context;
    private readonly IPowerBiConnectionService _connectionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PowerBiService> _logger;

    public PowerBiService(
        StatusDbContext context,
        IPowerBiConnectionService connectionService,
        IHttpClientFactory httpClientFactory,
        ILogger<PowerBiService> logger)
    {
        _context = context;
        _connectionService = connectionService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ---- Link management ----

    public async Task<PowerBiDatasetLinkDto?> GetLinkAsync(string entityId, string companyId)
    {
        var link = await _context.PowerBiDatasetLinks
            .Include(l => l.Connection)
            .FirstOrDefaultAsync(l => l.EntityId == entityId && l.CompanyId == companyId);

        return link == null ? null : ToDto(link);
    }

    public async Task<PowerBiDatasetLinkDto> SaveLinkAsync(string entityId, string companyId, PowerBiDatasetLinkRequest request, string? modifiedBy)
    {
        await EnsureDatasetEntityAsync(entityId, companyId);

        // Verify the chosen connection belongs to the same company.
        var connection = await _context.PowerBiConnections
            .FirstOrDefaultAsync(c => c.Id == request.PowerBiConnectionId && c.CompanyId == companyId)
            ?? throw new InvalidOperationException("Power BI connection not found for this company.");

        var link = await _context.PowerBiDatasetLinks
            .FirstOrDefaultAsync(l => l.EntityId == entityId && l.CompanyId == companyId);

        if (link == null)
        {
            link = new PowerBiDatasetLink
            {
                Id = Guid.NewGuid().ToString(),
                EntityId = entityId,
                CompanyId = companyId,
                CreatedBy = modifiedBy,
                CreatedOn = DateTime.UtcNow
            };
            _context.PowerBiDatasetLinks.Add(link);
        }

        link.PowerBiConnectionId = connection.Id;
        link.WorkspaceId = request.WorkspaceId.Trim();
        link.DatasetId = request.DatasetId.Trim();
        link.ModifiedBy = modifiedBy;
        link.ModifiedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        link.Connection = connection;
        return ToDto(link);
    }

    public async Task<bool> DeleteLinkAsync(string entityId, string companyId)
    {
        var link = await _context.PowerBiDatasetLinks
            .FirstOrDefaultAsync(l => l.EntityId == entityId && l.CompanyId == companyId);
        if (link == null) return false;

        _context.PowerBiDatasetLinks.Remove(link);
        await _context.SaveChangesAsync();
        return true;
    }

    // ---- Refresh history + trigger ----

    public async Task<List<PowerBiRefreshDto>> GetRefreshHistoryAsync(string entityId, string companyId, int top = 20, CancellationToken ct = default)
    {
        var (link, connection) = await ResolveAsync(entityId, companyId);
        var client = await CreateAuthorizedClientAsync(connection, ct);

        var url = $"{PowerBiApiBase}/groups/{link.WorkspaceId}/datasets/{link.DatasetId}/refreshes?$top={top}";
        var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Power BI returned {(int)response.StatusCode}: {Summarize(body)}");

        return ParseRefreshHistory(body);
    }

    public async Task<PowerBiActionResult> TriggerRefreshAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        try
        {
            var (link, connection) = await ResolveAsync(entityId, companyId);
            var client = await CreateAuthorizedClientAsync(connection, ct);

            var url = $"{PowerBiApiBase}/groups/{link.WorkspaceId}/datasets/{link.DatasetId}/refreshes";
            // Empty body → default (full) refresh. 202 Accepted means the refresh was queued.
            var response = await client.PostAsync(url, new StringContent("{\"notifyOption\":\"NoNotification\"}", System.Text.Encoding.UTF8, "application/json"), ct);

            if (response.IsSuccessStatusCode)
                return PowerBiActionResult.Ok("Refresh started.");

            var body = await response.Content.ReadAsStringAsync(ct);
            return PowerBiActionResult.Fail($"Power BI returned {(int)response.StatusCode}: {Summarize(body)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger Power BI refresh for entity {EntityId}", entityId);
            return PowerBiActionResult.Fail(ex.Message);
        }
    }

    public async Task<PowerBiRefreshScheduleDto?> GetRefreshScheduleAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var (link, connection) = await ResolveAsync(entityId, companyId);
        var client = await CreateAuthorizedClientAsync(connection, ct);

        var url = $"{PowerBiApiBase}/groups/{link.WorkspaceId}/datasets/{link.DatasetId}/refreshSchedule";
        var response = await client.GetAsync(url, ct);

        // A DirectQuery dataset (or one with no schedule) returns 404 — treat as "no schedule".
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Power BI returned {(int)response.StatusCode}: {Summarize(body)}");

        return ParseSchedule(body);
    }

    // ---- Lineage discovery ----

    public async Task<PowerBiDiscoveryDto> GetDiscoveryAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var (link, connection) = await ResolveAsync(entityId, companyId);
        var client = await CreateAuthorizedClientAsync(connection, ct);

        var dto = new PowerBiDiscoveryDto
        {
            DataSources = await GetDataSourcesAsync(client, link, ct)
        };

        try
        {
            dto.Tables = await GetDatasetTablesAsync(client, link, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Power BI table discovery failed for entity {EntityId}", entityId);
            dto.TablesError = ex.Message;
        }

        // Match discovered names to existing Database/Table entities in this company.
        var existing = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted
                        && (e.EntityType == AssetType.Database || e.EntityType == AssetType.Table))
            .Select(e => new { e.Id, e.Name, e.EntityType })
            .ToListAsync(ct);

        foreach (var ds in dto.DataSources.Where(d => !string.IsNullOrWhiteSpace(d.Database)))
        {
            var match = existing.FirstOrDefault(e => e.EntityType == AssetType.Database
                                                     && string.Equals(e.Name, ds.Database, StringComparison.OrdinalIgnoreCase));
            ds.ExistingEntityId = match?.Id;
            ds.ExistingEntityName = match?.Name;
        }

        foreach (var t in dto.Tables)
        {
            var match = existing.FirstOrDefault(e => e.EntityType == AssetType.Table
                                                     && string.Equals(e.Name, t.Name, StringComparison.OrdinalIgnoreCase));
            t.ExistingEntityId = match?.Id;
        }

        return dto;
    }

    public async Task<PowerBiLineageCommitResult> CommitLineageAsync(string entityId, string companyId, PowerBiLineageCommitRequest request, string? modifiedBy, CancellationToken ct = default)
    {
        await EnsureDatasetEntityAsync(entityId, companyId);

        var result = new PowerBiLineageCommitResult();
        var now = DateTime.UtcNow;

        // Existing Database/Table entities for this company, matched by name.
        var existing = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted
                        && (e.EntityType == AssetType.Database || e.EntityType == AssetType.Table))
            .ToListAsync(ct);

        MonitoredAsset? Find(string name, AssetType type) =>
            existing.FirstOrDefault(e => e.EntityType == type && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        // Dependencies already touching the dataset or the matched existing entities — so we don't duplicate edges.
        var relevantIds = existing.Select(e => e.Id).Append(entityId).ToHashSet();
        var existingDeps = await _context.AssetDependencies
            .Where(d => d.CompanyId == companyId && relevantIds.Contains(d.EntityId))
            .Select(d => new { d.EntityId, d.DependsOnEntityId })
            .ToListAsync(ct);

        var depPairs = existingDeps.Select(d => (d.EntityId, d.DependsOnEntityId)).ToHashSet();

        void AddDependency(string fromId, string toId, AssetType type, string description)
        {
            if (fromId == toId) return;
            if (!depPairs.Add((fromId, toId))) return; // already exists (or added this run)
            _context.AssetDependencies.Add(new AssetDependency
            {
                Id = Guid.NewGuid().ToString(),
                EntityId = fromId,
                DependsOnEntityId = toId,
                DependencyType = type,
                Description = description,
                IsActive = true,
                CompanyId = companyId,
                CreatedBy = modifiedBy,
                CreatedOn = now,
                ModifiedOn = now
            });
            result.DependenciesCreated++;
        }

        MonitoredAsset Upsert(string name, AssetType type, string? location, Action onAdd, Action onUpdate)
        {
            var entity = Find(name, type);
            if (entity == null)
            {
                entity = new MonitoredAsset
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    EntityType = type,
                    Location = location,
                    Description = "Discovered from Power BI dataset.",
                    IsActive = true,
                    CompanyId = companyId,
                    CreatedBy = modifiedBy,
                    CreatedOn = now,
                    ModifiedOn = now
                };
                _context.MonitoredAssets.Add(entity);
                existing.Add(entity); // so a later table reusing the same name matches this one
                onAdd();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(location)) entity.Location = location;
                entity.ModifiedBy = modifiedBy;
                entity.ModifiedOn = now;
                onUpdate();
            }
            return entity;
        }

        // Databases first — the dataset depends on each, and they anchor the tables.
        string? primaryDatabaseId = null;
        foreach (var db in request.Databases.Where(d => !string.IsNullOrWhiteSpace(d.Database)))
        {
            var dbEntity = Upsert(db.Database.Trim(), AssetType.Database, db.Server?.Trim(),
                () => result.DatabasesAdded++, () => result.DatabasesUpdated++);
            primaryDatabaseId ??= dbEntity.Id;
            AddDependency(entityId, dbEntity.Id, AssetType.Database, "Power BI data source");
        }

        // Tables — the dataset depends on each, and each depends on the (primary) database.
        foreach (var tableName in request.Tables.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var tableEntity = Upsert(tableName.Trim(), AssetType.Table, null,
                () => result.TablesAdded++, () => result.TablesUpdated++);
            AddDependency(entityId, tableEntity.Id, AssetType.Table, "Power BI dataset table");
            if (primaryDatabaseId != null)
                AddDependency(tableEntity.Id, primaryDatabaseId, AssetType.Database, "Table in database");
        }

        await _context.SaveChangesAsync(ct);

        result.Message =
            $"{result.DatabasesAdded} database(s) added, {result.DatabasesUpdated} updated; " +
            $"{result.TablesAdded} table(s) added, {result.TablesUpdated} updated; " +
            $"{result.DependenciesCreated} dependency link(s) created.";
        return result;
    }

    /// <summary>GET the dataset's data sources (server + database per source).</summary>
    private async Task<List<PowerBiDataSourceDto>> GetDataSourcesAsync(HttpClient client, PowerBiDatasetLink link, CancellationToken ct)
    {
        var url = $"{PowerBiApiBase}/groups/{link.WorkspaceId}/datasets/{link.DatasetId}/datasources";
        var response = await client.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Power BI returned {(int)response.StatusCode}: {Summarize(body)}");

        var list = new List<PowerBiDataSourceDto>();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in value.EnumerateArray())
        {
            string? server = null, database = null;
            if (item.TryGetProperty("connectionDetails", out var cd) && cd.ValueKind == JsonValueKind.Object)
            {
                server = GetString(cd, "server");
                database = GetString(cd, "database");
            }
            list.Add(new PowerBiDataSourceDto
            {
                DatasourceType = GetString(item, "datasourceType"),
                Server = server,
                Database = database
            });
        }
        return list;
    }

    // DAX strategies for enumerating model tables, tried in order — engines differ in which they accept.
    private static readonly string[] TableDaxQueries =
    {
        // Newest "friendly" metadata view.
        "EVALUATE SELECTCOLUMNS(FILTER(INFO.VIEW.TABLES(), NOT [IsHidden]), \"Name\", [Name])",
        // Current INFO functions.
        "EVALUATE SELECTCOLUMNS(FILTER(INFO.TABLES(), [IsHidden] = FALSE), \"Name\", [Name])",
        // Full INFO.TABLES() rows (parsed defensively, hidden filtered client-side).
        "EVALUATE INFO.TABLES()",
        // Pre-INFO fallback: distinct table names from column statistics (older engines accept this).
        "EVALUATE DISTINCT(SELECTCOLUMNS(COLUMNSTATISTICS(), \"Name\", [Table Name]))",
    };

    /// <summary>
    /// Discovers the dataset's visible model tables via the executeQueries DAX endpoint. Engines vary in which
    /// metadata functions they accept, so several DAX variants are tried; the first that runs wins.
    /// </summary>
    private async Task<List<PowerBiTableInfoDto>> GetDatasetTablesAsync(HttpClient client, PowerBiDatasetLink link, CancellationToken ct)
    {
        var url = $"{PowerBiApiBase}/groups/{link.WorkspaceId}/datasets/{link.DatasetId}/executeQueries";
        string? lastError = null;
        int lastStatus = 0;

        foreach (var dax in TableDaxQueries)
        {
            var payload = JsonSerializer.Serialize(new
            {
                queries = new[] { new { query = dax } },
                serializerSettings = new { includeNulls = true }
            });

            var response = await client.PostAsync(url, new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                lastStatus = (int)response.StatusCode;
                lastError = ExtractExecuteQueriesError(body);
                // 401/403 won't improve by trying another query — stop early.
                if (lastStatus is 401 or 403) break;
                continue;
            }

            var tables = ParseExecuteQueriesTables(body);
            if (tables.Count > 0) return tables;
            // Succeeded but empty — accept it (a model with no visible tables) rather than retry.
            return tables;
        }

        var hint = lastStatus is 401 or 403
            ? "Ensure the 'Dataset Execute Queries REST API' tenant setting is enabled for the service principal, and the principal has access to the dataset."
            : "This dataset doesn't support listing tables via DAX (executeQueries). You can add tables manually.";
        throw new InvalidOperationException($"Power BI returned {lastStatus}: {lastError ?? "Failed to execute the DAX query."} {hint}");
    }

    private static List<PowerBiTableInfoDto> ParseExecuteQueriesTables(string body)
    {
        var tables = new List<PowerBiTableInfoDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return tables;

        foreach (var res in results.EnumerateArray())
        {
            if (!res.TryGetProperty("tables", out var resTables) || resTables.ValueKind != JsonValueKind.Array) continue;
            foreach (var tbl in resTables.EnumerateArray())
            {
                if (!tbl.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array) continue;
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;

                    string? name = null;
                    bool isHidden = false;
                    foreach (var prop in row.EnumerateObject())
                    {
                        // Column keys come back bracketed, e.g. "[Name]" or "[IsHidden]".
                        var key = prop.Name.Trim('[', ']');
                        if (key.EndsWith("Name", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                            name ??= prop.Value.GetString();
                        else if (key.Contains("IsHidden", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.True)
                            isHidden = true;
                    }

                    if (isHidden || string.IsNullOrWhiteSpace(name)) continue;
                    // Skip the auto date tables Power BI generates.
                    if (name.StartsWith("LocalDateTable_", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("DateTableTemplate_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(name))
                        tables.Add(new PowerBiTableInfoDto { Name = name });
                }
            }
        }

        return tables.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Pulls the human-readable detail out of an executeQueries error body.</summary>
    private static string ExtractExecuteQueriesError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(empty response)";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                // error.pbi.error.details[].detail.value carries the readable message.
                if (error.TryGetProperty("pbi.error", out var pbi)
                    && pbi.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in details.EnumerateArray())
                        if (d.TryGetProperty("detail", out var detail) && GetString(detail, "value") is { Length: > 0 } v)
                            return v;
                }
                if (GetString(error, "message") is { Length: > 0 } m) return m;
                if (GetString(error, "code") is { Length: > 0 } c) return c;
            }
        }
        catch (JsonException) { /* fall through */ }
        return Summarize(body);
    }

    // ---- Internals ----

    /// <summary>Loads the entity's link + its connection (with decrypted secret), validating the entity is a Power BI dataset.</summary>
    private async Task<(PowerBiDatasetLink link, PowerBiConnection connection)> ResolveAsync(string entityId, string companyId)
    {
        await EnsureDatasetEntityAsync(entityId, companyId);

        var link = await _context.PowerBiDatasetLinks
            .FirstOrDefaultAsync(l => l.EntityId == entityId && l.CompanyId == companyId)
            ?? throw new InvalidOperationException("This dataset is not linked to a Power BI connection yet.");

        var connection = await _connectionService.GetForExecutionAsync(link.PowerBiConnectionId, companyId)
            ?? throw new InvalidOperationException("The linked Power BI connection no longer exists.");

        if (string.IsNullOrEmpty(connection.ClientSecretEncrypted))
            throw new InvalidOperationException("The linked Power BI connection has no client secret configured.");

        return (link, connection);
    }

    private async Task EnsureDatasetEntityAsync(string entityId, string companyId)
    {
        var entity = await _context.MonitoredAssets
            .FirstOrDefaultAsync(e => e.Id == entityId && e.CompanyId == companyId && !e.IsDeleted)
            ?? throw new InvalidOperationException("Dataset entity not found.");

        if (entity.EntityType != AssetType.Dataset)
            throw new InvalidOperationException("Power BI refresh is only available for Dataset entities.");

        if (string.IsNullOrEmpty(entity.Url) || !entity.Url.Contains("powerbi.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Power BI refresh requires the entity URL to point to powerbi.com.");
    }

    /// <summary>Acquires an app-only token via the client-credentials flow and returns a client with it attached.</summary>
    private async Task<HttpClient> CreateAuthorizedClientAsync(PowerBiConnection connection, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(connection, ct);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> GetAccessTokenAsync(PowerBiConnection connection, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var tokenUrl = $"https://login.microsoftonline.com/{connection.TenantId}/oauth2/v2.0/token";

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = connection.ClientId,
            // GetForExecutionAsync decrypts the secret in-memory before we read it here.
            ["client_secret"] = connection.ClientSecretEncrypted ?? string.Empty,
            ["scope"] = PowerBiScope
        });

        var response = await client.PostAsync(tokenUrl, form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure AD token request failed ({(int)response.StatusCode}): {Summarize(body)}");

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenEl) && tokenEl.GetString() is { Length: > 0 } token)
            return token;

        throw new InvalidOperationException("Azure AD token response did not contain an access token.");
    }

    private static List<PowerBiRefreshDto> ParseRefreshHistory(string body)
    {
        var results = new List<PowerBiRefreshDto>();
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in value.EnumerateArray())
        {
            results.Add(new PowerBiRefreshDto
            {
                RequestId = GetString(item, "requestId"),
                RefreshType = GetString(item, "refreshType"),
                Status = GetString(item, "status") ?? "Unknown",
                StartTime = GetDate(item, "startTime"),
                EndTime = GetDate(item, "endTime"),
                ErrorMessage = ExtractError(GetString(item, "serviceExceptionJson"))
            });
        }

        return results;
    }

    private static PowerBiRefreshScheduleDto ParseSchedule(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var dto = new PowerBiRefreshScheduleDto
        {
            Enabled = root.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True,
            LocalTimeZoneId = GetString(root, "localTimeZoneId"),
            NotifyOption = GetString(root, "notifyOption"),
            Days = GetStringArray(root, "days"),
            Times = GetStringArray(root, "times")
        };

        dto.NextRefresh = ComputeNextRefresh(dto.Enabled, dto.Days, dto.Times, dto.LocalTimeZoneId);
        return dto;
    }

    /// <summary>Computes the next scheduled refresh instant (UTC) from Power BI's day/time/timezone schedule.</summary>
    private static DateTime? ComputeNextRefresh(bool enabled, List<string> days, List<string> times, string? tzId)
    {
        if (!enabled || days.Count == 0 || times.Count == 0) return null;

        TimeZoneInfo tz;
        try { tz = string.IsNullOrWhiteSpace(tzId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { tz = TimeZoneInfo.Utc; }

        var daySet = new HashSet<DayOfWeek>();
        foreach (var d in days)
            if (Enum.TryParse<DayOfWeek>(d, true, out var dow)) daySet.Add(dow);

        var parsedTimes = new List<TimeSpan>();
        foreach (var t in times)
            if (TimeSpan.TryParse(t, out var ts)) parsedTimes.Add(ts);
        parsedTimes.Sort();

        if (daySet.Count == 0 || parsedTimes.Count == 0) return null;

        var nowUtc = DateTimeOffset.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        // Look ahead up to a week (covers any weekly schedule).
        for (var offset = 0; offset <= 7; offset++)
        {
            var date = nowLocal.Date.AddDays(offset);
            if (!daySet.Contains(date.DayOfWeek)) continue;

            foreach (var ts in parsedTimes)
            {
                var localWall = date + ts; // wall-clock time in the schedule's timezone
                var candidate = new DateTimeOffset(localWall, tz.GetUtcOffset(localWall));
                if (candidate > nowUtc) return candidate.UtcDateTime;
            }
        }

        return null;
    }

    private static List<string> GetStringArray(JsonElement el, string name)
    {
        var list = new List<string>();
        if (el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var item in arr.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s) list.Add(s);
        return list;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTime? GetDate(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && p.TryGetDateTime(out var dt) ? dt : null;

    /// <summary>Pulls a readable message out of Power BI's serviceExceptionJson blob.</summary>
    private static string? ExtractError(string? serviceExceptionJson)
    {
        if (string.IsNullOrWhiteSpace(serviceExceptionJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(serviceExceptionJson);
            if (doc.RootElement.TryGetProperty("errorDescription", out var d) && d.GetString() is { } desc)
                return desc;
            if (doc.RootElement.TryGetProperty("errorCode", out var c) && c.GetString() is { } code)
                return code;
        }
        catch (JsonException) { /* fall through to raw text */ }
        return serviceExceptionJson;
    }

    private static string Summarize(string body) =>
        string.IsNullOrWhiteSpace(body) ? "(empty response)" : body.Length > 500 ? body[..500] : body;

    private static PowerBiDatasetLinkDto ToDto(PowerBiDatasetLink l) => new()
    {
        Id = l.Id,
        EntityId = l.EntityId,
        PowerBiConnectionId = l.PowerBiConnectionId,
        ConnectionName = l.Connection?.Name,
        WorkspaceId = l.WorkspaceId,
        DatasetId = l.DatasetId
    };
}

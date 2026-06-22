using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Application.Shared.Services;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace Application.Shared.Services.Data;

/// <summary>
/// Executes and manages scheduled ingestion sources: fetches data from an external DB / REST API /
/// Blob / SFTP into a temp file, then loads it into the dataset's DuckDB table via the shared import
/// core. Used by both the web app ("Run now") and the Hangfire scheduler job, so it lives in Shared.
/// </summary>
public interface IIngestionService
{
    Task<List<IngestionSourceDto>> GetSourcesAsync(string companyId, string datasetId, CancellationToken ct = default);
    Task<IngestionSourceDto?> GetSourceAsync(string companyId, string id, CancellationToken ct = default);
    Task<IngestionSourceDto> CreateAsync(string companyId, string datasetId, string? userId, SaveIngestionSourceRequest request, CancellationToken ct = default);
    Task<IngestionSourceDto?> UpdateAsync(string companyId, string id, SaveIngestionSourceRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string companyId, string id, CancellationToken ct = default);
    Task<List<IngestionRunDto>> GetRunsAsync(string companyId, string sourceId, int limit = 20, CancellationToken ct = default);

    /// <summary>Runs one ingestion source end-to-end. Never throws — failures are captured in the run record.</summary>
    Task<ImportResult> RunSourceAsync(string sourceId, CancellationToken ct = default);
}

public class IngestionService : IIngestionService
{
    private readonly ApplicationDbContext _db;
    private readonly IDuckdbService _duckdb;
    private readonly IDatabaseTableService _dbTables;
    private readonly ICredentialProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IngestionService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public IngestionService(
        ApplicationDbContext db,
        IDuckdbService duckdb,
        IDatabaseTableService dbTables,
        ICredentialProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<IngestionService> logger)
    {
        _db = db;
        _duckdb = duckdb;
        _dbTables = dbTables;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ---- CRUD ----

    public async Task<List<IngestionSourceDto>> GetSourcesAsync(string companyId, string datasetId, CancellationToken ct = default)
    {
        var sources = await _db.IngestionSource
            .Where(s => s.CompanyId == companyId && s.DatasetId == datasetId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return sources.Select(ToDto).ToList();
    }

    public async Task<IngestionSourceDto?> GetSourceAsync(string companyId, string id, CancellationToken ct = default)
    {
        var source = await _db.IngestionSource.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, ct);
        return source == null ? null : ToDto(source);
    }

    public async Task<IngestionSourceDto> CreateAsync(string companyId, string datasetId, string? userId, SaveIngestionSourceRequest request, CancellationToken ct = default)
    {
        var source = new IngestionSource
        {
            CompanyId = companyId,
            DatasetId = datasetId,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        ApplyRequest(source, request, isCreate: true);
        _db.IngestionSource.Add(source);
        await _db.SaveChangesAsync(ct);
        return ToDto(source);
    }

    public async Task<IngestionSourceDto?> UpdateAsync(string companyId, string id, SaveIngestionSourceRequest request, CancellationToken ct = default)
    {
        var source = await _db.IngestionSource.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, ct);
        if (source == null) return null;

        ApplyRequest(source, request, isCreate: false);
        source.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(source);
    }

    public async Task<bool> DeleteAsync(string companyId, string id, CancellationToken ct = default)
    {
        var source = await _db.IngestionSource.FirstOrDefaultAsync(s => s.Id == id && s.CompanyId == companyId, ct);
        if (source == null) return false;

        // No cascade deletes in this codebase — remove run history explicitly first.
        var runs = await _db.IngestionRun.Where(r => r.SourceId == id).ToListAsync(ct);
        _db.IngestionRun.RemoveRange(runs);
        _db.IngestionSource.Remove(source);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<IngestionRunDto>> GetRunsAsync(string companyId, string sourceId, int limit = 20, CancellationToken ct = default)
    {
        return await _db.IngestionRun
            .Where(r => r.SourceId == sourceId && r.CompanyId == companyId)
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .Select(r => new IngestionRunDto
            {
                Id = r.Id,
                StartedAt = r.StartedAt,
                FinishedAt = r.FinishedAt,
                Status = r.Status,
                RowsIngested = r.RowsIngested,
                ErrorMessage = r.ErrorMessage
            })
            .ToListAsync(ct);
    }

    private void ApplyRequest(IngestionSource source, SaveIngestionSourceRequest request, bool isCreate)
    {
        source.TargetTable = request.TargetTable;
        source.Name = request.Name;
        source.Description = request.Description;
        source.SourceKind = request.SourceKind;
        source.SourceEntityId = request.SourceEntityId;
        source.SourceConfig = JsonSerializer.Serialize(request.Config ?? new IngestionSourceConfig());
        source.ImportMode = request.ImportMode;
        source.KeyColumns = request.KeyColumns is { Count: > 0 } ? string.Join(",", request.KeyColumns) : null;
        source.CreateIfNotExists = request.CreateIfNotExists;
        source.IncrementalColumn = string.IsNullOrWhiteSpace(request.IncrementalColumn) ? null : request.IncrementalColumn;
        source.CronExpression = request.CronExpression;
        source.TimeZone = request.TimeZone;
        source.IsEnabled = request.IsEnabled;

        // A non-null secret replaces the stored one; null leaves the existing secret untouched.
        if (request.Secret != null)
            source.SecretEncrypted = string.IsNullOrEmpty(request.Secret) ? null : _protector.Encrypt(request.Secret);
    }

    private static IngestionSourceConfig ParseConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new IngestionSourceConfig();
        try { return JsonSerializer.Deserialize<IngestionSourceConfig>(json, JsonOpts) ?? new IngestionSourceConfig(); }
        catch { return new IngestionSourceConfig(); }
    }

    private static IngestionSourceDto ToDto(IngestionSource s) => new()
    {
        Id = s.Id,
        DatasetId = s.DatasetId,
        TargetTable = s.TargetTable,
        Name = s.Name,
        Description = s.Description,
        SourceKind = s.SourceKind,
        SourceEntityId = s.SourceEntityId,
        Config = ParseConfig(s.SourceConfig),
        HasSecret = !string.IsNullOrEmpty(s.SecretEncrypted),
        ImportMode = s.ImportMode,
        KeyColumns = string.IsNullOrWhiteSpace(s.KeyColumns)
            ? new List<string>()
            : s.KeyColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        CreateIfNotExists = s.CreateIfNotExists,
        IncrementalColumn = s.IncrementalColumn,
        IncrementalLastValue = s.IncrementalLastValue,
        CronExpression = s.CronExpression,
        TimeZone = s.TimeZone,
        IsEnabled = s.IsEnabled,
        LastRunAt = s.LastRunAt,
        LastRunStatus = s.LastRunStatus,
        LastRunMessage = s.LastRunMessage,
        LastRunRows = s.LastRunRows,
        CreatedAt = s.CreatedAt
    };

    // ---- Execution ----

    public async Task<ImportResult> RunSourceAsync(string sourceId, CancellationToken ct = default)
    {
        var source = await _db.IngestionSource.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
        if (source == null)
            return new ImportResult { Error = "Ingestion source not found." };

        var run = new IngestionRun
        {
            SourceId = source.Id,
            CompanyId = source.CompanyId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };
        _db.IngestionRun.Add(run);
        await _db.SaveChangesAsync(ct);

        string? tempPath = null;
        ImportResult result;
        try
        {
            var config = ParseConfig(source.SourceConfig);
            (tempPath, var format) = await FetchToTempAsync(source, config, ct);

            var mode = Enum.TryParse<ImportMode>(source.ImportMode, ignoreCase: true, out var m) ? m : ImportMode.Append;
            var keys = string.IsNullOrWhiteSpace(source.KeyColumns)
                ? new List<string>()
                : source.KeyColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            await using (var stream = File.OpenRead(tempPath))
            {
                result = await _duckdb.ImportFileAsync(source.DatasetId, source.TargetTable, stream, format, mode, keys,
                    skipInvalidRows: true, createIfMissing: source.CreateIfNotExists, ct);
            }

            // Advance the watermark to the highest value now present in the target table.
            if (result.Success && !string.IsNullOrWhiteSpace(source.IncrementalColumn))
            {
                var newWatermark = await ReadMaxAsync(source.DatasetId, source.TargetTable, source.IncrementalColumn!, ct);
                if (newWatermark != null)
                    source.IncrementalLastValue = newWatermark;
            }

            run.Status = result.Success ? "Success" : "Failed";
            run.ErrorMessage = result.Error;
            run.RowsIngested = result.RowsInserted + result.RowsUpdated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion source {SourceId} failed.", sourceId);
            result = new ImportResult { Error = ex.Message };
            run.Status = "Failed";
            run.ErrorMessage = ex.Message;
        }
        finally
        {
            run.FinishedAt = DateTime.UtcNow;
            source.LastRunAt = run.FinishedAt;
            source.LastRunStatus = run.Status;
            source.LastRunMessage = run.ErrorMessage;
            source.LastRunRows = run.RowsIngested;
            await _db.SaveChangesAsync(ct);
            TryDelete(tempPath);
        }

        return result;
    }

    // Fetches the source's data to a temp file and reports its format.
    private async Task<(string path, ImportFileFormat format)> FetchToTempAsync(IngestionSource source, IngestionSourceConfig config, CancellationToken ct)
    {
        var kind = Enum.TryParse<IngestionSourceKind>(source.SourceKind, ignoreCase: true, out var k) ? k : IngestionSourceKind.ExternalDatabase;
        return kind switch
        {
            IngestionSourceKind.ExternalDatabase => await FetchFromDatabaseAsync(source, config, ct),
            IngestionSourceKind.Rest => await FetchFromRestAsync(source, config, ct),
            IngestionSourceKind.Blob => await FetchFromBlobAsync(source, config, ct),
            IngestionSourceKind.Sftp => await FetchFromSftpAsync(source, config, ct),
            _ => throw new NotSupportedException($"Unsupported ingestion source kind: {source.SourceKind}")
        };
    }

    private async Task<(string, ImportFileFormat)> FetchFromDatabaseAsync(IngestionSource source, IngestionSourceConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(source.SourceEntityId))
            throw new InvalidOperationException("This database source has no linked database entity.");

        var conn = await _dbTables.GetDecryptedConnectionAsync(source.SourceEntityId!, source.CompanyId, ct);
        if (conn == null)
            throw new InvalidOperationException("No connection is configured on the source database entity.");

        var baseQuery = !string.IsNullOrWhiteSpace(config.Query)
            ? config.Query!.TrimEnd().TrimEnd(';')
            : $"SELECT * FROM {QualifyTable(config.Schema, config.Table)}";

        // Incremental: only pull rows newer than the last watermark.
        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(source.IncrementalColumn) && !string.IsNullOrWhiteSpace(source.IncrementalLastValue))
            query = $"SELECT * FROM ({baseQuery}) _src WHERE _src.{source.IncrementalColumn} > {IncrementalLiteral(source.IncrementalLastValue!)}";

        var path = TempPath(".csv");
        await _dbTables.ReadToTempCsvAsync(conn, query, path, ct);
        return (path, ImportFileFormat.Csv);
    }

    private async Task<(string, ImportFileFormat)> FetchFromRestAsync(IngestionSource source, IngestionSourceConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            throw new InvalidOperationException("REST source has no URL configured.");

        var client = _httpClientFactory.CreateClient();
        var method = string.IsNullOrWhiteSpace(config.Method) ? HttpMethod.Get : new HttpMethod(config.Method!.ToUpperInvariant());
        using var request = new HttpRequestMessage(method, config.Url);

        if (config.Headers != null)
            foreach (var (key, value) in config.Headers)
                request.Headers.TryAddWithoutValidation(key, value);

        var secret = DecryptSecret(source);
        switch ((config.AuthType ?? "none").ToLowerInvariant())
        {
            case "bearer":
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                break;
            case "basic":
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{secret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
                break;
        }

        if (!string.IsNullOrEmpty(config.Body) && method != HttpMethod.Get)
            request.Content = new StringContent(config.Body!, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"REST request failed ({(int)response.StatusCode}): {Truncate(body)}");

        using var doc = JsonDocument.Parse(body);
        var element = NavigateJsonPath(doc.RootElement, config.JsonPath);

        var path = TempPath(".json");
        var rawJson = element.ValueKind == JsonValueKind.Array
            ? element.GetRawText()
            : "[" + element.GetRawText() + "]";
        await File.WriteAllTextAsync(path, rawJson, new UTF8Encoding(false), ct);
        return (path, ImportFileFormat.Json);
    }

    private async Task<(string, ImportFileFormat)> FetchFromBlobAsync(IngestionSource source, IngestionSourceConfig config, CancellationToken ct)
    {
        var connectionString = DecryptSecret(source);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Blob source has no connection string/SAS configured (set the secret).");
        if (string.IsNullOrWhiteSpace(config.Container) || string.IsNullOrWhiteSpace(config.BlobPath))
            throw new InvalidOperationException("Blob source requires a container and blob path.");

        var container = new BlobContainerClient(connectionString, config.Container);
        var blob = container.GetBlobClient(config.BlobPath);

        var format = ResolveFileFormat(config.FileFormat, config.BlobPath!);
        var path = TempPath(ExtFor(format));
        await blob.DownloadToAsync(path, ct);
        return (path, format);
    }

    private async Task<(string, ImportFileFormat)> FetchFromSftpAsync(IngestionSource source, IngestionSourceConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.RemotePath))
            throw new InvalidOperationException("SFTP source requires a host and remote path.");

        var secret = DecryptSecret(source);
        var format = ResolveFileFormat(config.FileFormat, config.RemotePath!);
        var path = TempPath(ExtFor(format));

        // SSH.NET is synchronous; run off the request/scheduler thread.
        await Task.Run(() =>
        {
            using var sftp = new SftpClient(config.Host, config.Port ?? 22, config.SftpUsername ?? "", secret ?? "");
            sftp.Connect();
            try
            {
                using var fs = File.Create(path);
                sftp.DownloadFile(config.RemotePath, fs);
            }
            finally
            {
                sftp.Disconnect();
            }
        }, ct);

        return (path, format);
    }

    // Highest value of the watermark column now in the target table (as a string for storage).
    private async Task<string?> ReadMaxAsync(string datasetId, string tableName, string column, CancellationToken ct)
    {
        var sql = $"SELECT MAX(\"{column.Replace("\"", "\"\"")}\") AS m FROM \"{tableName.Replace("\"", "\"\"")}\"";
        var r = await _duckdb.ExecuteSqlAsync(datasetId, sql, allowWrite: false, maxRows: 1, ct);
        if (!string.IsNullOrEmpty(r.Error) || r.Rows.Count == 0) return null;

        var value = r.Rows[0].Values.FirstOrDefault();
        return value switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private string? DecryptSecret(IngestionSource source) =>
        string.IsNullOrEmpty(source.SecretEncrypted) ? null : _protector.Decrypt(source.SecretEncrypted);

    private static string QualifyTable(string? schema, string? table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new InvalidOperationException("Database source requires a query or a table name.");
        return string.IsNullOrWhiteSpace(schema) ? table! : $"{schema}.{table}";
    }

    // Numeric watermarks are emitted unquoted; everything else (timestamps, ids as strings) is quoted.
    private static string IncrementalLiteral(string lastValue)
    {
        if (long.TryParse(lastValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
            double.TryParse(lastValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return lastValue;
        return "'" + lastValue.Replace("'", "''") + "'";
    }

    private static JsonElement NavigateJsonPath(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
                current = next;
            else
                throw new InvalidOperationException($"JSON path segment '{segment}' not found in the response.");
        }
        return current;
    }

    private static ImportFileFormat ResolveFileFormat(string? explicitFormat, string path)
    {
        if (!string.IsNullOrWhiteSpace(explicitFormat) && Enum.TryParse<ImportFileFormat>(explicitFormat, ignoreCase: true, out var fmt))
            return fmt;
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tsv" => ImportFileFormat.Tsv,
            ".json" => ImportFileFormat.Json,
            ".parquet" => ImportFileFormat.Parquet,
            ".xlsx" => ImportFileFormat.Excel,
            _ => ImportFileFormat.Csv
        };
    }

    private static string ExtFor(ImportFileFormat format) => format switch
    {
        ImportFileFormat.Tsv => ".tsv",
        ImportFileFormat.Json => ".json",
        ImportFileFormat.Parquet => ".parquet",
        ImportFileFormat.Excel => ".xlsx",
        _ => ".csv"
    };

    private static string TempPath(string ext) => Path.Combine(Path.GetTempPath(), $"ingest_{Guid.NewGuid()}{ext}");

    private static void TryDelete(string? path)
    {
        try { if (path != null && File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }

    private static string Truncate(string s, int max = 500) => s.Length <= max ? s : s.Substring(0, max);
}

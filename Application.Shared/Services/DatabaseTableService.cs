using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;

namespace Application.Shared.Services;

public class DatabaseTableService : IDatabaseTableService
{
    private readonly StatusDbContext _context;
    private readonly ICredentialProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DatabaseTableService> _logger;

    public DatabaseTableService(
        StatusDbContext context,
        ICredentialProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<DatabaseTableService> logger)
    {
        _context = context;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ---- Connection CRUD ----

    public async Task<DatabaseConnectionDto?> GetConnectionAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var connection = await _context.DatabaseConnections
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        return connection == null ? null : ToDto(connection);
    }

    public async Task<DatabaseConnectionDto> SaveConnectionAsync(string entityId, string companyId, DatabaseConnectionRequest request, string? modifiedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var connection = await _context.DatabaseConnections
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);

        var port = request.Port > 0 ? request.Port : DefaultPort(request.DatabaseType);

        if (connection == null)
        {
            connection = new DatabaseConnection
            {
                Id = Guid.NewGuid().ToString(),
                EntityId = entityId,
                CompanyId = companyId,
                DatabaseType = request.DatabaseType,
                Host = request.Host,
                Port = port,
                DatabaseName = request.DatabaseName,
                Username = request.Username,
                SecretEncrypted = string.IsNullOrEmpty(request.Secret) ? null : _protector.Encrypt(request.Secret),
                UseSsl = request.UseSsl,
                FilePath = request.FilePath,
                CreatedBy = modifiedBy,
                CreatedOn = now,
                ModifiedOn = now
            };
            _context.DatabaseConnections.Add(connection);
        }
        else
        {
            connection.DatabaseType = request.DatabaseType;
            connection.Host = request.Host;
            connection.Port = port;
            connection.DatabaseName = request.DatabaseName;
            connection.Username = request.Username;
            connection.UseSsl = request.UseSsl;
            connection.FilePath = request.FilePath;
            connection.ModifiedBy = modifiedBy;
            connection.ModifiedOn = now;

            // Only replace the secret when a new one is supplied; blank keeps the existing one.
            if (!string.IsNullOrEmpty(request.Secret))
                connection.SecretEncrypted = _protector.Encrypt(request.Secret);
        }

        await _context.SaveChangesAsync(ct);
        return ToDto(connection);
    }

    public async Task<bool> DeleteConnectionAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var connection = await _context.DatabaseConnections
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        if (connection == null) return false;

        _context.DatabaseConnections.Remove(connection);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ---- Test + discovery ----

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var connection = await LoadDecryptedAsync(entityId, companyId, ct);
        if (connection == null)
            return new DatabaseConnectionTestResult { Ok = false, Error = "No connection is configured for this entity." };

        try
        {
            await ListTablesAsync(connection, ct);
            return new DatabaseConnectionTestResult { Ok = true };
        }
        catch (Exception ex)
        {
            return new DatabaseConnectionTestResult { Ok = false, Error = ex.Message };
        }
    }

    public async Task<DatabaseTableDiscoveryDto> DiscoverTablesAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var connection = await LoadDecryptedAsync(entityId, companyId, ct);
        if (connection == null)
            throw new InvalidOperationException("No connection is configured for this entity. Configure and save it first.");

        var dto = new DatabaseTableDiscoveryDto();
        List<(string Schema, string Name)> tables;
        try
        {
            tables = await ListTablesAsync(connection, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database table discovery failed for entity {EntityId}", entityId);
            dto.Error = ex.Message;
            return dto;
        }

        dto.Tables = tables
            .Select(t => new DatabaseTableInfoDto
            {
                Schema = t.Schema,
                Name = t.Name,
                FullName = string.IsNullOrEmpty(t.Schema) ? t.Name : $"{t.Schema}.{t.Name}"
            })
            .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Match each discovered table to an existing Table entity (same company, name OrdinalIgnoreCase).
        var existing = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted && e.EntityType == AssetType.Table)
            .Select(e => new { e.Id, e.Name })
            .ToListAsync(ct);

        foreach (var t in dto.Tables)
        {
            var match = existing.FirstOrDefault(e => string.Equals(e.Name, t.FullName, StringComparison.OrdinalIgnoreCase));
            t.ExistingEntityId = match?.Id;
        }

        return dto;
    }

    // ---- Commit (mirrors PowerBiService.CommitLineageAsync) ----

    public async Task<DatabaseTableCommitResult> CommitTablesAsync(string entityId, string companyId, DatabaseTableCommitRequest request, string? modifiedBy, CancellationToken ct = default)
    {
        var dbEntity = await _context.MonitoredAssets
            .FirstOrDefaultAsync(e => e.Id == entityId && e.CompanyId == companyId && !e.IsDeleted, ct);
        if (dbEntity == null)
            throw new InvalidOperationException("Database entity not found.");

        var result = new DatabaseTableCommitResult();
        var now = DateTime.UtcNow;

        var existing = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && !e.IsDeleted && e.EntityType == AssetType.Table)
            .ToListAsync(ct);

        MonitoredAsset? Find(string name) =>
            existing.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        var relevantIds = existing.Select(e => e.Id).Append(entityId).ToHashSet();
        var existingDeps = await _context.AssetDependencies
            .Where(d => d.CompanyId == companyId && relevantIds.Contains(d.EntityId))
            .Select(d => new { d.EntityId, d.DependsOnEntityId })
            .ToListAsync(ct);
        var depPairs = existingDeps.Select(d => (d.EntityId, d.DependsOnEntityId)).ToHashSet();

        void AddDependency(string fromId, string toId, AssetType type, string description)
        {
            if (fromId == toId) return;
            if (!depPairs.Add((fromId, toId))) return;

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

        foreach (var fullName in request.Tables
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .Select(t => t.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var table = Find(fullName);
            if (table == null)
            {
                table = new MonitoredAsset
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = fullName,
                    EntityType = AssetType.Table,
                    Description = "Discovered from database.",
                    IsActive = true,
                    CompanyId = companyId,
                    CreatedBy = modifiedBy,
                    CreatedOn = now,
                    ModifiedOn = now
                };
                _context.MonitoredAssets.Add(table);
                existing.Add(table);
                result.TablesAdded++;
            }
            else
            {
                table.ModifiedBy = modifiedBy;
                table.ModifiedOn = now;
                result.TablesUpdated++;
            }

            // The table depends on this database entity.
            AddDependency(table.Id, entityId, AssetType.Database, "Table in database");
        }

        await _context.SaveChangesAsync(ct);

        result.Message =
            $"{result.TablesAdded} table(s) added, {result.TablesUpdated} updated; " +
            $"{result.DependenciesCreated} dependency link(s) created.";
        return result;
    }

    // ---- Table freshness checks ----

    public async Task<TableCheckDto?> GetTableCheckAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var check = await _context.TableChecks.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        var hasConn = await HasParentConnectionAsync(entityId, companyId, ct);

        // Always return a DTO (defaults when unconfigured) so the UI can show the form and the
        // "needs a database connection" hint before anything is saved.
        return new TableCheckDto
        {
            EntityId = entityId,
            FreshnessColumn = check?.FreshnessColumn,
            MaxAgeMinutes = check?.MaxAgeMinutes ?? 1440,
            IsEnabled = check?.IsEnabled ?? false,
            HasDatabaseConnection = hasConn
        };
    }

    public async Task<TableCheckDto> SaveTableCheckAsync(string entityId, string companyId, TableCheckRequest request, string? modifiedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var check = await _context.TableChecks
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);

        if (check == null)
        {
            check = new TableCheck
            {
                Id = Guid.NewGuid().ToString(),
                EntityId = entityId,
                CompanyId = companyId,
                CreatedBy = modifiedBy,
                CreatedOn = now
            };
            _context.TableChecks.Add(check);
        }

        check.FreshnessColumn = request.FreshnessColumn?.Trim();
        check.MaxAgeMinutes = request.MaxAgeMinutes > 0 ? request.MaxAgeMinutes : 1440;
        check.IsEnabled = request.IsEnabled;
        check.ModifiedBy = modifiedBy;
        check.ModifiedOn = now;

        await _context.SaveChangesAsync(ct);

        return new TableCheckDto
        {
            EntityId = entityId,
            FreshnessColumn = check.FreshnessColumn,
            MaxAgeMinutes = check.MaxAgeMinutes,
            IsEnabled = check.IsEnabled,
            HasDatabaseConnection = await HasParentConnectionAsync(entityId, companyId, ct)
        };
    }

    public async Task<bool> DeleteTableCheckAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var check = await _context.TableChecks
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        if (check == null) return false;

        _context.TableChecks.Remove(check);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TableFreshnessResult> RunFreshnessCheckAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var check = await _context.TableChecks.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        if (check == null || string.IsNullOrWhiteSpace(check.FreshnessColumn))
            return new TableFreshnessResult { Ok = false, Error = "Configure a freshness column and save it first." };

        var table = await _context.MonitoredAssets.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entityId && e.CompanyId == companyId, ct);
        if (table == null)
            return new TableFreshnessResult { Ok = false, Error = "Table entity not found." };

        var connection = await GetDecryptedParentConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return new TableFreshnessResult { Ok = false, Error = "No database connection found on the parent database entity. Configure it there first." };

        return await CheckFreshnessAsync(connection, table.Name, check.FreshnessColumn!, check.MaxAgeMinutes, ct);
    }

    // ---- Bulk read for ingestion (no DbContext) ----

    public async Task<int> ReadToTempCsvAsync(DatabaseConnection c, string query, string destCsvPath, CancellationToken ct = default)
    {
        // ClickHouse has no ADO driver — ask it for CSV directly over the read-only HTTP endpoint.
        if (c.DatabaseType == DataSourceType.ClickHouse)
        {
            var csvQuery = $"{query.TrimEnd().TrimEnd(';')} FORMAT CSVWithNames";
            var body = await QueryClickHouseAsync(c, csvQuery, ct);
            await File.WriteAllTextAsync(destCsvPath, body, new UTF8Encoding(false), ct);
            // Row count = lines minus the header (best-effort).
            var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(0, lines - 1);
        }

        await using var connection = CreateAdoConnection(c);
        await connection.OpenAsync(ct);

        var readOnlySetup = ReadOnlySetupFor(c.DatabaseType);
        if (!string.IsNullOrEmpty(readOnlySetup))
        {
            await using var setup = connection.CreateCommand();
            setup.CommandText = readOnlySetup;
            await setup.ExecuteNonQueryAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = query;
        await using var reader = await command.ExecuteReaderAsync(ct);

        await using var writer = new StreamWriter(destCsvPath, false, new UTF8Encoding(false));

        // Header
        var fieldCount = reader.FieldCount;
        for (int i = 0; i < fieldCount; i++)
        {
            if (i > 0) await writer.WriteAsync(',');
            await writer.WriteAsync(CsvEscape(reader.GetName(i)));
        }
        await writer.WriteAsync('\n');

        var rows = 0;
        while (await reader.ReadAsync(ct))
        {
            for (int i = 0; i < fieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(',');
                if (!reader.IsDBNull(i))
                    await writer.WriteAsync(CsvEscape(FormatCsvValue(reader.GetValue(i))));
            }
            await writer.WriteAsync('\n');
            rows++;
        }

        return rows;
    }

    // Formats a value for a CSV cell in a way DuckDB can TRY_CAST back to a typed column.
    private static string FormatCsvValue(object value) => value switch
    {
        null or DBNull => string.Empty,
        bool b => b ? "true" : "false",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    // RFC-4180 quoting: wrap in quotes and double internal quotes when the value contains , " CR or LF.
    private static string CsvEscape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ---- Pure probes (no DbContext; safe to call concurrently from the ping job) ----

    public async Task<DatabaseProbeResult> ProbeConnectionAsync(DatabaseConnection c, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (c.DatabaseType == DataSourceType.ClickHouse)
                await QueryClickHouseAsync(c, "SELECT 1", ct);
            else
                await ExecuteScalarAsync(CreateAdoConnection(c), "SELECT 1", ReadOnlySetupFor(c.DatabaseType), ct);

            sw.Stop();
            return new DatabaseProbeResult { Ok = true, ResponseMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2) };
        }
        catch (Exception ex)
        {
            return new DatabaseProbeResult { Ok = false, Error = Truncate(ex.Message) };
        }
    }

    public async Task<TableFreshnessResult> CheckFreshnessAsync(DatabaseConnection c, string tableFullName, string freshnessColumn, int maxAgeMinutes, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            object? maxVal;
            long rowCount;

            if (c.DatabaseType == DataSourceType.ClickHouse)
                (maxVal, rowCount) = await QueryClickHouseFreshnessAsync(c, tableFullName, freshnessColumn, ct);
            else
                (maxVal, rowCount) = await QueryFreshnessAdoAsync(
                    CreateAdoConnection(c),
                    BuildFreshnessSql(c.DatabaseType, tableFullName, freshnessColumn),
                    ReadOnlySetupFor(c.DatabaseType), ct);

            sw.Stop();

            var result = new TableFreshnessResult
            {
                Ok = true,
                ResponseMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                RowCount = rowCount,
                LastUpdatedUtc = CoerceToUtc(maxVal)
            };

            if (result.LastUpdatedUtc.HasValue)
            {
                var age = (DateTime.UtcNow - result.LastUpdatedUtc.Value).TotalMinutes;
                result.AgeMinutes = Math.Round(age, 2);
                result.IsStale = age > maxAgeMinutes;
            }

            return result;
        }
        catch (Exception ex)
        {
            return new TableFreshnessResult { Ok = false, Error = Truncate(ex.Message) };
        }
    }

    public Task<DatabaseConnection?> GetDecryptedConnectionAsync(string entityId, string companyId, CancellationToken ct = default)
        => LoadDecryptedAsync(entityId, companyId, ct);

    public async Task<DatabaseConnection?> GetDecryptedParentConnectionAsync(string tableEntityId, string companyId, CancellationToken ct = default)
    {
        var parentId = await GetParentDatabaseEntityIdAsync(tableEntityId, companyId, ct);
        return parentId == null ? null : await LoadDecryptedAsync(parentId, companyId, ct);
    }

    // ---- Internals ----

    /// <summary>The id of the Database entity a Table entity depends on (the "Table in database" edge).</summary>
    private async Task<string?> GetParentDatabaseEntityIdAsync(string tableEntityId, string companyId, CancellationToken ct) =>
        await _context.AssetDependencies
            .Where(d => d.EntityId == tableEntityId && d.CompanyId == companyId
                        && d.DependencyType == AssetType.Database && d.IsActive)
            .Select(d => d.DependsOnEntityId)
            .FirstOrDefaultAsync(ct);

    private async Task<bool> HasParentConnectionAsync(string tableEntityId, string companyId, CancellationToken ct)
    {
        var parentId = await GetParentDatabaseEntityIdAsync(tableEntityId, companyId, ct);
        return parentId != null &&
               await _context.DatabaseConnections.AnyAsync(c => c.EntityId == parentId && c.CompanyId == companyId, ct);
    }

    private async Task<DatabaseConnection?> LoadDecryptedAsync(string entityId, string companyId, CancellationToken ct)
    {
        var connection = await _context.DatabaseConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        if (connection == null) return null;

        // Decrypt the secret in-memory for execution; never persist or return this to the client.
        if (!string.IsNullOrEmpty(connection.SecretEncrypted))
            connection.SecretEncrypted = _protector.Decrypt(connection.SecretEncrypted);

        return connection;
    }

    /// <summary>Dispatches to the engine-specific table listing. The password is read from
    /// <see cref="DatabaseConnection.SecretEncrypted"/> which the caller has decrypted in-memory.</summary>
    private async Task<List<(string Schema, string Name)>> ListTablesAsync(DatabaseConnection c, CancellationToken ct)
    {
        // Every path is strictly read-only: only SELECT against metadata, and the session/connection
        // is opened read-only where the engine supports it (readOnlySetup runs before the listing query).
        if (c.DatabaseType == DataSourceType.ClickHouse)
            return await QueryClickHouseTablesAsync(c, ct);

        var sql = c.DatabaseType switch
        {
            DataSourceType.SQLServer => "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
            DataSourceType.PostgreSQL => "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')",
            DataSourceType.MySQL => $"SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema = '{EscapeLiteral(c.DatabaseName)}'",
            DataSourceType.DuckDB => "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE'",
            _ => throw new NotSupportedException($"Unsupported database type: {c.DatabaseType}.")
        };

        return await QueryTablesAsync(CreateAdoConnection(c), sql, ct, ReadOnlySetupFor(c.DatabaseType));
    }

    /// <summary>Builds an unopened ADO.NET connection for an engine. ClickHouse is HTTP-only (no ADO).</summary>
    private static DbConnection CreateAdoConnection(DatabaseConnection c) => c.DatabaseType switch
    {
        DataSourceType.SQLServer => new SqlConnection(BuildSqlServerConnectionString(c)),
        DataSourceType.PostgreSQL => new NpgsqlConnection(BuildPostgresConnectionString(c)),
        DataSourceType.MySQL => new MySqlConnection(BuildMySqlConnectionString(c)),
        DataSourceType.DuckDB => new DuckDBConnection(BuildDuckDbConnectionString(c)),
        _ => throw new NotSupportedException($"No ADO.NET driver for database type: {c.DatabaseType}.")
    };

    /// <summary>Command to put the session into read-only mode before querying, where the engine supports it.</summary>
    private static string? ReadOnlySetupFor(DataSourceType type) => type switch
    {
        DataSourceType.PostgreSQL => "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY",
        DataSourceType.MySQL => "SET SESSION TRANSACTION READ ONLY",
        _ => null
    };

    /// <summary>Opens an ADO.NET connection, runs a two-column (schema, name) query, and returns the rows.
    /// <paramref name="readOnlySetup"/>, when set, runs first to put the session into read-only mode.</summary>
    private static async Task<List<(string Schema, string Name)>> QueryTablesAsync(DbConnection connection, string sql, CancellationToken ct, string? readOnlySetup = null)
    {
        var tables = new List<(string, string)>();
        await using (connection)
        {
            await connection.OpenAsync(ct);

            if (!string.IsNullOrEmpty(readOnlySetup))
            {
                await using var setup = connection.CreateCommand();
                setup.CommandText = readOnlySetup;
                await setup.ExecuteNonQueryAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var schema = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString() ?? string.Empty;
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1)?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(name))
                    tables.Add((schema, name));
            }
        }
        return tables;
    }

    /// <summary>Runs a read-only ClickHouse query over HTTP (readonly=1) and returns the raw response body.</summary>
    private async Task<string> QueryClickHouseAsync(DatabaseConnection c, string query, CancellationToken ct)
    {
        var protocol = c.UseSsl ? "https" : "http";
        // readonly=1 makes the ClickHouse session reject any write/DDL — these queries only need reads.
        var url = $"{protocol}://{c.Host}:{c.Port}/?readonly=1";

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(c.Username))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Username}:{c.SecretEncrypted}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ClickHouse query failed ({(int)response.StatusCode}): {body}");
        return body;
    }

    private async Task<List<(string Schema, string Name)>> QueryClickHouseTablesAsync(DatabaseConnection c, CancellationToken ct)
    {
        const string query = "SELECT database, name FROM system.tables WHERE database NOT IN ('system', 'INFORMATION_SCHEMA', 'information_schema') FORMAT JSONEachRow";
        var body = await QueryClickHouseAsync(c, query, ct);

        var tables = new List<(string, string)>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                if (row == null) continue;
                var schema = row.TryGetValue("database", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                var name = row.TryGetValue("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(name))
                    tables.Add((schema, name));
            }
            catch
            {
                // Skip malformed lines.
            }
        }
        return tables;
    }

    /// <summary>Opens a connection read-only and runs a scalar query (e.g. SELECT 1), discarding the result.</summary>
    private static async Task ExecuteScalarAsync(DbConnection connection, string sql, string? readOnlySetup, CancellationToken ct)
    {
        await using (connection)
        {
            await connection.OpenAsync(ct);

            if (!string.IsNullOrEmpty(readOnlySetup))
            {
                await using var setup = connection.CreateCommand();
                setup.CommandText = readOnlySetup;
                await setup.ExecuteNonQueryAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteScalarAsync(ct);
        }
    }

    /// <summary>Runs the two-column freshness query and returns (MAX value, row count) from the first row.</summary>
    private static async Task<(object? MaxValue, long RowCount)> QueryFreshnessAdoAsync(DbConnection connection, string sql, string? readOnlySetup, CancellationToken ct)
    {
        await using (connection)
        {
            await connection.OpenAsync(ct);

            if (!string.IsNullOrEmpty(readOnlySetup))
            {
                await using var setup = connection.CreateCommand();
                setup.CommandText = readOnlySetup;
                await setup.ExecuteNonQueryAsync(ct);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var maxVal = reader.IsDBNull(0) ? null : reader.GetValue(0);
                var rowCount = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1));
                return (maxVal, rowCount);
            }
            return (null, 0L);
        }
    }

    private async Task<(object? MaxValue, long RowCount)> QueryClickHouseFreshnessAsync(DatabaseConnection c, string tableFullName, string column, CancellationToken ct)
    {
        var table = QuoteQualified(DataSourceType.ClickHouse, tableFullName);
        var col = QuoteIdentifier(DataSourceType.ClickHouse, column);
        // toString() so JSONEachRow yields plain strings we can parse uniformly across column types.
        var query = $"SELECT toString(max({col})) AS last_updated, toString(count()) AS row_count FROM {table} FORMAT JSONEachRow";

        var body = await QueryClickHouseAsync(c, query, ct);
        var line = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (line == null) return (null, 0L);

        var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
        object? maxVal = null;
        var rowCount = 0L;
        if (row != null)
        {
            if (row.TryGetValue("last_updated", out var m))
            {
                var s = m.GetString();
                maxVal = string.IsNullOrEmpty(s) ? null : s;
            }
            if (row.TryGetValue("row_count", out var rc) && long.TryParse(rc.GetString(), out var parsed))
                rowCount = parsed;
        }
        return (maxVal, rowCount);
    }

    private static string BuildFreshnessSql(DataSourceType type, string tableFullName, string column)
    {
        var table = QuoteQualified(type, tableFullName);
        var col = QuoteIdentifier(type, column);
        // SQL Server's COUNT(*) is int and overflows past ~2.1B rows; COUNT_BIG(*) is bigint.
        var countExpr = type == DataSourceType.SQLServer ? "COUNT_BIG(*)" : "COUNT(*)";
        return $"SELECT MAX({col}) AS last_updated, {countExpr} AS row_count FROM {table}";
    }

    /// <summary>Quotes a possibly-qualified "{schema}.{table}" name, splitting on the first dot.</summary>
    private static string QuoteQualified(DataSourceType type, string fullName)
    {
        var idx = fullName.IndexOf('.');
        if (idx <= 0 || idx == fullName.Length - 1)
            return QuoteIdentifier(type, fullName);
        return $"{QuoteIdentifier(type, fullName[..idx])}.{QuoteIdentifier(type, fullName[(idx + 1)..])}";
    }

    /// <summary>Quotes a single identifier for the engine, escaping the quote char (neutralizes injection).</summary>
    private static string QuoteIdentifier(DataSourceType type, string id) => type switch
    {
        DataSourceType.SQLServer => $"[{id.Replace("]", "]]")}]",
        DataSourceType.PostgreSQL => $"\"{id.Replace("\"", "\"\"")}\"",
        DataSourceType.DuckDB => $"\"{id.Replace("\"", "\"\"")}\"",
        DataSourceType.MySQL => $"`{id.Replace("`", "``")}`",
        DataSourceType.ClickHouse => $"`{id.Replace("`", "``")}`",
        _ => id
    };

    /// <summary>Best-effort coercion of a MAX(timestamp) value to UTC. Timestamps are assumed to be UTC.</summary>
    private static DateTime? CoerceToUtc(object? value)
    {
        switch (value)
        {
            case null or DBNull:
                return null;
            case DateTime dt:
                return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            case DateTimeOffset dto:
                return dto.UtcDateTime;
            default:
                return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : null;
        }
    }

    private static string Truncate(string s) => string.IsNullOrEmpty(s) || s.Length <= 300 ? s : s[..300];

    private static string BuildSqlServerConnectionString(DatabaseConnection c)
    {
        var server = c.Port > 0 ? $"{c.Host},{c.Port}" : c.Host;
        // ApplicationIntent=ReadOnly signals read-only intent (and routes to a readable secondary on AlwaysOn);
        // combined with SELECT-only queries the feature never writes. Harmless/ignored on standalone servers.
        return $"Server={server};Initial Catalog={c.DatabaseName};User ID={c.Username};Password={c.SecretEncrypted};" +
               $"Encrypt={(c.UseSsl ? "True" : "False")};TrustServerCertificate=True;ApplicationIntent=ReadOnly;Connection Timeout=15;";
    }

    private static string BuildPostgresConnectionString(DatabaseConnection c) =>
        $"Host={c.Host};Port={(c.Port > 0 ? c.Port : 5432)};Database={c.DatabaseName};Username={c.Username};Password={c.SecretEncrypted};" +
        $"SSL Mode={(c.UseSsl ? "Require" : "Prefer")};Trust Server Certificate=true;Timeout=15;";

    private static string BuildMySqlConnectionString(DatabaseConnection c) =>
        $"Server={c.Host};Port={(c.Port > 0 ? c.Port : 3306)};Database={c.DatabaseName};User ID={c.Username};Password={c.SecretEncrypted};" +
        $"SslMode={(c.UseSsl ? "Required" : "Preferred")};Connection Timeout=15;";

    private static string BuildDuckDbConnectionString(DatabaseConnection c) =>
        // READ_ONLY opens the file without taking a write lock and rejects any modification.
        $"DataSource={c.FilePath};ACCESS_MODE=READ_ONLY";

    private static string EscapeLiteral(string? value) => (value ?? string.Empty).Replace("'", "''");

    private static int DefaultPort(DataSourceType type) => type switch
    {
        DataSourceType.SQLServer => 1433,
        DataSourceType.PostgreSQL => 5432,
        DataSourceType.MySQL => 3306,
        DataSourceType.ClickHouse => 8123,
        _ => 0
    };

    private static DatabaseConnectionDto ToDto(DatabaseConnection c) => new()
    {
        Id = c.Id,
        EntityId = c.EntityId,
        DatabaseType = c.DatabaseType,
        Host = c.Host,
        Port = c.Port,
        DatabaseName = c.DatabaseName,
        Username = c.Username,
        UseSsl = c.UseSsl,
        FilePath = c.FilePath,
        HasSecret = !string.IsNullOrEmpty(c.SecretEncrypted)
    };
}

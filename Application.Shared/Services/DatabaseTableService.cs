using System.Data.Common;
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

    // ---- Internals ----

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
        return c.DatabaseType switch
        {
            DataSourceType.SQLServer => await QueryTablesAsync(new SqlConnection(BuildSqlServerConnectionString(c)),
                "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", ct),
            DataSourceType.PostgreSQL => await QueryTablesAsync(new NpgsqlConnection(BuildPostgresConnectionString(c)),
                "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')", ct,
                readOnlySetup: "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY"),
            DataSourceType.MySQL => await QueryTablesAsync(new MySqlConnection(BuildMySqlConnectionString(c)),
                $"SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema = '{EscapeLiteral(c.DatabaseName)}'", ct,
                readOnlySetup: "SET SESSION TRANSACTION READ ONLY"),
            DataSourceType.DuckDB => await QueryTablesAsync(new DuckDBConnection(BuildDuckDbConnectionString(c)),
                "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE'", ct),
            DataSourceType.ClickHouse => await QueryClickHouseTablesAsync(c, ct),
            _ => throw new NotSupportedException($"Unsupported database type: {c.DatabaseType}.")
        };
    }

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

    private async Task<List<(string Schema, string Name)>> QueryClickHouseTablesAsync(DatabaseConnection c, CancellationToken ct)
    {
        var protocol = c.UseSsl ? "https" : "http";
        // readonly=1 makes the ClickHouse session reject any write/DDL — listing tables only needs reads.
        var url = $"{protocol}://{c.Host}:{c.Port}/?readonly=1";

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(c.Username))
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Username}:{c.SecretEncrypted}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        const string query = "SELECT database, name FROM system.tables WHERE database NOT IN ('system', 'INFORMATION_SCHEMA', 'information_schema') FORMAT JSONEachRow";
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ClickHouse query failed ({(int)response.StatusCode}): {body}");

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

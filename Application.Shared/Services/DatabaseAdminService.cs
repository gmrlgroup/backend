using System.Data.Common;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Services;

/// <summary>
/// Space checks and user provisioning against the external databases registered as Database-type entities.
/// <para>
/// This is the only place in the codebase that issues DDL to a customer database, so a few rules are
/// absolute:
/// </para>
/// <list type="bullet">
/// <item>Usernames reach the SQL as identifiers and cannot be parameterized. Every one is checked against
/// <see cref="UsernamePattern"/> <b>and</b> quoted for the dialect. Both, never one or the other.</item>
/// <item>Passwords are also unparameterizable in <c>CREATE LOGIN</c>/<c>CREATE USER</c> on every engine here,
/// so they are validated to contain no quote, backslash or control character before being escaped.</item>
/// <item>No generated password is ever persisted, logged, or echoed into an error message.</item>
/// </list>
/// </summary>
public class DatabaseAdminService : IDatabaseAdminService
{
    private readonly StatusDbContext _context;
    private readonly ICredentialProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DatabaseAdminService> _logger;

    /// <summary>Cap on how many per-table size rows we hand back — big warehouses have tens of thousands.</summary>
    private const int MaxTablesReported = 500;

    private const int AdminCommandTimeoutSeconds = 60;

    /// <summary>
    /// Conservative identifier shape for a database principal. Deliberately narrower than what the engines
    /// accept: it excludes quotes, whitespace, semicolons and comment markers, so a name can never terminate
    /// the identifier it is embedded in. Mirrors the defensive regex on <c>SshServerExecutor</c>'s service name.
    /// </summary>
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z0-9_][A-Za-z0-9_$.\-]{0,62}$", RegexOptions.Compiled);

    /// <summary>MySQL principals are user@host; the host half allows wildcards and IP/CIDR punctuation.</summary>
    private static readonly Regex MySqlHostPattern = new(@"^[A-Za-z0-9_%.\-:]{1,255}$", RegexOptions.Compiled);

    public DatabaseAdminService(
        StatusDbContext context,
        ICredentialProtector protector,
        IHttpClientFactory httpClientFactory,
        ILogger<DatabaseAdminService> logger)
    {
        _context = context;
        _protector = protector;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ---- Entity listing ----

    public async Task<List<DatabaseAdminEntityDto>> GetDatabaseEntitiesAsync(string companyId, CancellationToken ct = default)
    {
        // Left joins, not the inner join DatabaseTableService.GetConnectedDatabasesAsync uses: this page has
        // to show databases that aren't configured yet so the operator knows what's missing.
        var rows = await (
            from e in _context.MonitoredAssets.AsNoTracking()
            where e.CompanyId == companyId && !e.IsDeleted && e.EntityType == AssetType.Database
            // Joined on company as well as entity — the entity filter alone would be enough given the
            // unique index, but every query in this codebase is company-scoped and this one is no exception.
            join c in _context.DatabaseConnections.AsNoTracking()
                on new { Id = e.Id, Company = e.CompanyId } equals new { Id = c.EntityId, Company = c.CompanyId } into conns
            from c in conns.DefaultIfEmpty()
            join a in _context.DatabaseAdminCredentials.AsNoTracking()
                on new { Id = e.Id, Company = e.CompanyId } equals new { Id = a.EntityId, Company = a.CompanyId } into admins
            from a in admins.DefaultIfEmpty()
            orderby e.Name
            select new
            {
                e.Id,
                e.Name,
                Connection = c,
                HasAdmin = a != null && a.SecretEncrypted != null
            }).ToListAsync(ct);

        return rows.Select(r => new DatabaseAdminEntityDto
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
            DatabaseType = r.Connection?.DatabaseType ?? DataSourceType.SQLServer,
            Host = r.Connection?.Host,
            DatabaseName = r.Connection?.DatabaseName,
            FilePath = r.Connection?.FilePath,
            HasConnection = r.Connection != null,
            HasAdminCredential = r.HasAdmin,
            Capabilities = IDatabaseAdminService.CapabilitiesFor(r.Connection?.DatabaseType ?? DataSourceType.SQLServer)
        }).ToList();
    }

    // ---- Admin credential CRUD ----

    public async Task<DatabaseAdminCredentialDto?> GetAdminCredentialAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var credential = await _context.DatabaseAdminCredentials.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EntityId == entityId && a.CompanyId == companyId, ct);
        return credential == null ? null : ToDto(credential);
    }

    public async Task<DatabaseAdminCredentialDto> SaveAdminCredentialAsync(string entityId, string companyId, DatabaseAdminCredentialRequest request, string? modifiedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var credential = await _context.DatabaseAdminCredentials
            .FirstOrDefaultAsync(a => a.EntityId == entityId && a.CompanyId == companyId, ct);

        if (credential == null)
        {
            credential = new DatabaseAdminCredential
            {
                Id = Guid.NewGuid().ToString(),
                EntityId = entityId,
                CompanyId = companyId,
                Username = request.Username,
                SecretEncrypted = string.IsNullOrEmpty(request.Secret) ? null : _protector.Encrypt(request.Secret),
                CreatedBy = modifiedBy,
                CreatedOn = now,
                ModifiedOn = now
            };
            _context.DatabaseAdminCredentials.Add(credential);
        }
        else
        {
            credential.Username = request.Username;
            credential.ModifiedBy = modifiedBy;
            credential.ModifiedOn = now;

            // Only replace the secret when a new one is supplied; blank keeps the existing one.
            if (!string.IsNullOrEmpty(request.Secret))
                credential.SecretEncrypted = _protector.Encrypt(request.Secret);
        }

        await _context.SaveChangesAsync(ct);
        return ToDto(credential);
    }

    public async Task<bool> DeleteAdminCredentialAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var credential = await _context.DatabaseAdminCredentials
            .FirstOrDefaultAsync(a => a.EntityId == entityId && a.CompanyId == companyId, ct);
        if (credential == null) return false;

        _context.DatabaseAdminCredentials.Remove(credential);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    // ---- Size ----

    public async Task<DatabaseSizeDto> GetSizeAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var connection = await LoadDecryptedConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return new DatabaseSizeDto { Error = "No connection is configured for this database." };

        try
        {
            return connection.DatabaseType switch
            {
                DataSourceType.SQLServer => await GetSqlServerSizeAsync(connection, ct),
                DataSourceType.PostgreSQL => await GetPostgresSizeAsync(connection, ct),
                DataSourceType.MySQL => await GetMySqlSizeAsync(connection, ct),
                DataSourceType.ClickHouse => await GetClickHouseSizeAsync(connection, ct),
                DataSourceType.DuckDB => GetDuckDbSize(connection),
                _ => new DatabaseSizeDto { Error = $"Size checks are not supported for {connection.DatabaseType}." }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database size check failed for entity {EntityId}", entityId);
            return new DatabaseSizeDto { Error = Truncate(ex.Message) };
        }
    }

    private async Task<DatabaseSizeDto> GetSqlServerSizeAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        // size and FILEPROPERTY are in 8KB pages. FILEPROPERTY is NULL for files that aren't in the current
        // database context, hence the ISNULL guard.
        const string totalsSql = @"
SELECT
    SUM(CAST(size AS BIGINT)) * 8192 AS total_bytes,
    SUM(CASE WHEN type_desc = 'ROWS' THEN CAST(size AS BIGINT) ELSE 0 END) * 8192 AS data_bytes,
    SUM(CASE WHEN type_desc = 'LOG'  THEN CAST(size AS BIGINT) ELSE 0 END) * 8192 AS log_bytes,
    SUM(CAST(size AS BIGINT) - ISNULL(CAST(FILEPROPERTY(name, 'SpaceUsed') AS BIGINT), 0)) * 8192 AS free_bytes
FROM sys.database_files";

        var dto = new DatabaseSizeDto();
        var totals = await ReadRowsAsync(conn, totalsSql, ct);
        if (totals.Count > 0)
        {
            dto.TotalBytes = ToLong(totals[0][0]);
            dto.DataBytes = ToLong(totals[0][1]);
            dto.LogBytes = ToLong(totals[0][2]);
            dto.FreeBytes = ToLong(totals[0][3]);
        }

        var tablesSql = $@"
SELECT TOP {MaxTablesReported}
    s.name AS schema_name,
    t.name AS table_name,
    SUM(CASE WHEN i.index_id < 2 THEN p.rows ELSE 0 END) AS row_count,
    SUM(a.total_pages) * 8192 AS total_bytes
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.partitions p ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.allocation_units a ON a.container_id = p.partition_id
GROUP BY s.name, t.name
ORDER BY SUM(a.total_pages) DESC";

        foreach (var row in await ReadRowsAsync(conn, tablesSql, ct))
            dto.Tables.Add(new TableSizeDto
            {
                Schema = ToStr(row[0]),
                Name = ToStr(row[1]),
                RowCount = ToLong(row[2]),
                TotalBytes = ToLong(row[3]) ?? 0
            });

        return dto;
    }

    private async Task<DatabaseSizeDto> GetPostgresSizeAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        var dto = new DatabaseSizeDto();
        var totals = await ReadRowsAsync(conn, "SELECT pg_database_size(current_database())", ct);
        if (totals.Count > 0)
        {
            dto.TotalBytes = ToLong(totals[0][0]);
            dto.DataBytes = dto.TotalBytes;
        }

        var tablesSql = $@"
SELECT n.nspname, c.relname,
       COALESCE(st.n_live_tup, 0) AS row_count,
       pg_total_relation_size(c.oid) AS total_bytes
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_stat_user_tables st ON st.relid = c.oid
WHERE c.relkind IN ('r', 'p')
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
ORDER BY pg_total_relation_size(c.oid) DESC
LIMIT {MaxTablesReported}";

        foreach (var row in await ReadRowsAsync(conn, tablesSql, ct))
            dto.Tables.Add(new TableSizeDto
            {
                Schema = ToStr(row[0]),
                Name = ToStr(row[1]),
                RowCount = ToLong(row[2]),
                TotalBytes = ToLong(row[3]) ?? 0
            });

        return dto;
    }

    private async Task<DatabaseSizeDto> GetMySqlSizeAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        var db = ExternalConnectionFactory.EscapeLiteral(c.DatabaseName);
        var sql = $@"
SELECT table_schema, table_name, table_rows,
       COALESCE(data_length, 0) + COALESCE(index_length, 0) AS total_bytes
FROM information_schema.tables
WHERE table_schema = '{db}' AND table_type = 'BASE TABLE'
ORDER BY total_bytes DESC
LIMIT {MaxTablesReported}";

        var dto = new DatabaseSizeDto();
        foreach (var row in await ReadRowsAsync(conn, sql, ct))
            dto.Tables.Add(new TableSizeDto
            {
                Schema = ToStr(row[0]),
                Name = ToStr(row[1]),
                // table_rows is an estimate for InnoDB, not an exact count — that's the cheap-metadata trade-off.
                RowCount = ToLong(row[2]),
                TotalBytes = ToLong(row[3]) ?? 0
            });

        // information_schema only accounts for the tables we just listed, so total = their sum. Read the
        // unfiltered total separately in case the per-table list was capped.
        var totals = await ReadRowsAsync(conn,
            $"SELECT SUM(COALESCE(data_length,0) + COALESCE(index_length,0)) FROM information_schema.tables WHERE table_schema = '{db}'", ct);
        if (totals.Count > 0)
        {
            dto.TotalBytes = ToLong(totals[0][0]);
            dto.DataBytes = dto.TotalBytes;
        }

        return dto;
    }

    private async Task<DatabaseSizeDto> GetClickHouseSizeAsync(DatabaseConnection c, CancellationToken ct)
    {
        var db = ExternalConnectionFactory.EscapeLiteral(c.DatabaseName);
        var where = string.IsNullOrWhiteSpace(c.DatabaseName)
            ? "active AND database NOT IN ('system', 'INFORMATION_SCHEMA', 'information_schema')"
            : $"active AND database = '{db}'";

        var query = $@"SELECT database, table, sum(rows) AS row_count, sum(bytes_on_disk) AS total_bytes
FROM system.parts WHERE {where}
GROUP BY database, table ORDER BY total_bytes DESC LIMIT {MaxTablesReported} FORMAT JSONEachRow";

        var dto = new DatabaseSizeDto();
        long total = 0;
        foreach (var row in ParseJsonEachRow(await QueryClickHouseAsync(c, query, readOnly: true, ct)))
        {
            var bytes = ToLong(JsonValue(row, "total_bytes")) ?? 0;
            total += bytes;
            dto.Tables.Add(new TableSizeDto
            {
                Schema = ToStr(JsonValue(row, "database")),
                Name = ToStr(JsonValue(row, "table")),
                RowCount = ToLong(JsonValue(row, "row_count")),
                TotalBytes = bytes
            });
        }

        dto.TotalBytes = total;
        dto.DataBytes = total;
        return dto;
    }

    private static DatabaseSizeDto GetDuckDbSize(DatabaseConnection c)
    {
        var dto = new DatabaseSizeDto();
        if (string.IsNullOrWhiteSpace(c.FilePath) || !File.Exists(c.FilePath))
            return new DatabaseSizeDto { Error = "The DuckDB file path is not set or the file does not exist." };

        dto.TotalBytes = new FileInfo(c.FilePath).Length;
        dto.DataBytes = dto.TotalBytes;

        // Opened read-only, so this never takes a write lock on a file another process may be using.
        using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        conn.Open();
        using var command = conn.CreateCommand();
        command.CommandText = $"SELECT schema_name, table_name, estimated_size FROM duckdb_tables() ORDER BY estimated_size DESC LIMIT {MaxTablesReported}";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            dto.Tables.Add(new TableSizeDto
            {
                Schema = reader.IsDBNull(0) ? string.Empty : ToStr(reader.GetValue(0)),
                Name = reader.IsDBNull(1) ? string.Empty : ToStr(reader.GetValue(1)),
                // duckdb_tables() reports an estimated row count, not bytes; there is no per-table byte figure.
                RowCount = reader.IsDBNull(2) ? null : ToLong(reader.GetValue(2)),
                TotalBytes = 0
            });

        return dto;
    }

    // ---- List users ----

    public async Task<DatabaseUserListDto> ListUsersAsync(string entityId, string companyId, CancellationToken ct = default)
    {
        var (connection, error) = await LoadAdminConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return new DatabaseUserListDto { Error = error };

        if (connection.DatabaseType == DataSourceType.DuckDB)
            return new DatabaseUserListDto { Error = DuckDbUnsupported };

        try
        {
            return connection.DatabaseType switch
            {
                DataSourceType.SQLServer => new DatabaseUserListDto { Users = await ListSqlServerUsersAsync(connection, ct) },
                DataSourceType.PostgreSQL => new DatabaseUserListDto { Users = await ListPostgresUsersAsync(connection, ct) },
                DataSourceType.MySQL => new DatabaseUserListDto { Users = await ListMySqlUsersAsync(connection, ct) },
                DataSourceType.ClickHouse => new DatabaseUserListDto { Users = await ListClickHouseUsersAsync(connection, ct) },
                _ => new DatabaseUserListDto { Error = $"User management is not supported for {connection.DatabaseType}." }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Listing database users failed for entity {EntityId}", entityId);
            return new DatabaseUserListDto { Error = Truncate(ex.Message) };
        }
    }

    private async Task<List<DatabaseUserDto>> ListSqlServerUsersAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        // sys.server_principals is only readable with server-level rights; LEFT JOIN so a database-scoped
        // admin still gets the user list, just without the is_disabled flag.
        const string sql = @"
SELECT p.name,
       p.type_desc,
       ISNULL(sp.is_disabled, 0) AS is_disabled,
       ISNULL(STUFF((SELECT ',' + r.name
                     FROM sys.database_role_members m
                     JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
                     WHERE m.member_principal_id = p.principal_id
                     FOR XML PATH('')), 1, 1, ''), '') AS roles
FROM sys.database_principals p
LEFT JOIN sys.server_principals sp ON sp.sid = p.sid
WHERE p.type IN ('S', 'U', 'G')
  AND p.name NOT IN ('dbo', 'guest', 'INFORMATION_SCHEMA', 'sys')
  AND p.is_fixed_role = 0
ORDER BY p.name";

        return (await ReadRowsAsync(conn, sql, ct)).Select(row => new DatabaseUserDto
        {
            Name = ToStr(row[0]),
            Type = ToStr(row[1]),
            IsDisabled = ToLong(row[2]) == 1,
            Roles = SplitCsv(ToStr(row[3]))
        }).ToList();
    }

    private async Task<List<DatabaseUserDto>> ListPostgresUsersAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        const string sql = @"
SELECT r.rolname,
       r.rolcanlogin,
       r.rolsuper,
       COALESCE(string_agg(g.rolname, ','), '') AS roles
FROM pg_roles r
LEFT JOIN pg_auth_members m ON m.member = r.oid
LEFT JOIN pg_roles g ON g.oid = m.roleid
WHERE r.rolname NOT LIKE 'pg\_%'
GROUP BY r.rolname, r.rolcanlogin, r.rolsuper
ORDER BY r.rolname";

        return (await ReadRowsAsync(conn, sql, ct)).Select(row =>
        {
            var canLogin = row[1] is bool b && b;
            var isSuper = row[2] is bool s && s;
            var roles = SplitCsv(ToStr(row[3]));
            if (isSuper) roles.Insert(0, "SUPERUSER");
            return new DatabaseUserDto
            {
                Name = ToStr(row[0]),
                Type = canLogin ? "LOGIN ROLE" : "GROUP ROLE",
                // A role that can't log in isn't disabled as such, but for the operator it's the same signal.
                IsDisabled = !canLogin,
                Roles = roles
            };
        }).ToList();
    }

    private async Task<List<DatabaseUserDto>> ListMySqlUsersAsync(DatabaseConnection c, CancellationToken ct)
    {
        await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
        await conn.OpenAsync(ct);

        var users = (await ReadRowsAsync(conn,
            "SELECT user, host, plugin, account_locked FROM mysql.user ORDER BY user, host", ct))
            .Select(row => new DatabaseUserDto
            {
                Name = $"{ToStr(row[0])}@{ToStr(row[1])}",
                Type = ToStr(row[2]),
                IsDisabled = string.Equals(ToStr(row[3]), "Y", StringComparison.OrdinalIgnoreCase)
            }).ToList();

        // Privileges live in a separate view; grantee comes back already quoted as 'user'@'host'.
        var db = ExternalConnectionFactory.EscapeLiteral(c.DatabaseName);
        var grants = await ReadRowsAsync(conn,
            $@"SELECT grantee, GROUP_CONCAT(DISTINCT privilege_type) FROM information_schema.schema_privileges
WHERE table_schema = '{db}' GROUP BY grantee", ct);

        foreach (var row in grants)
        {
            var grantee = ToStr(row[0]).Replace("'", string.Empty);
            var match = users.FirstOrDefault(u => string.Equals(u.Name, grantee, StringComparison.OrdinalIgnoreCase));
            match?.Roles.AddRange(SplitCsv(ToStr(row[1])));
        }

        return users;
    }

    private async Task<List<DatabaseUserDto>> ListClickHouseUsersAsync(DatabaseConnection c, CancellationToken ct)
    {
        var users = ParseJsonEachRow(await QueryClickHouseAsync(c,
            "SELECT name, auth_type FROM system.users ORDER BY name FORMAT JSONEachRow", readOnly: true, ct))
            .Select(row => new DatabaseUserDto
            {
                Name = ToStr(JsonValue(row, "name")),
                Type = ToStr(JsonValue(row, "auth_type"))
            }).ToList();

        var db = ExternalConnectionFactory.EscapeLiteral(c.DatabaseName);
        var where = string.IsNullOrWhiteSpace(c.DatabaseName) ? "1" : $"database = '{db}'";
        var grants = ParseJsonEachRow(await QueryClickHouseAsync(c,
            $"SELECT user_name, groupUniqArray(access_type) AS privileges FROM system.grants WHERE {where} GROUP BY user_name FORMAT JSONEachRow",
            readOnly: true, ct));

        foreach (var row in grants)
        {
            var name = ToStr(JsonValue(row, "user_name"));
            var match = users.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match == null) continue;

            if (row.TryGetValue("privileges", out var privileges) && privileges.ValueKind == JsonValueKind.Array)
                match.Roles.AddRange(privileges.EnumerateArray().Select(p => p.GetString() ?? string.Empty).Where(p => p.Length > 0));
        }

        return users;
    }

    // ---- Create user ----

    public async Task<DatabaseUserOperationResult> CreateUserAsync(string entityId, string companyId, CreateDatabaseUserRequest request, CancellationToken ct = default)
    {
        var (connection, error) = await LoadAdminConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return Fail(request.Username, error!);

        var (username, host, nameError) = ParsePrincipal(request.Username, connection.DatabaseType);
        if (nameError != null) return Fail(request.Username, nameError);

        var password = request.Password;
        var generated = string.IsNullOrEmpty(password);
        if (generated) password = GeneratePassword();
        if (!IsUsablePasswordLiteral(password!))
            return Fail(username, "The password contains a character that cannot be used safely (quote, backslash or control character), or is not between 12 and 128 characters.");

        try
        {
            var message = connection.DatabaseType switch
            {
                DataSourceType.SQLServer => await CreateSqlServerUserAsync(connection, username, password!, request, ct),
                DataSourceType.PostgreSQL => await CreatePostgresUserAsync(connection, username, password!, request.AccessLevel, ct),
                DataSourceType.MySQL => await CreateMySqlUserAsync(connection, username, host, password!, request.AccessLevel, ct),
                DataSourceType.ClickHouse => await CreateClickHouseUserAsync(connection, username, password!, request.AccessLevel, ct),
                DataSourceType.DuckDB => throw new NotSupportedException(DuckDbUnsupported),
                _ => throw new NotSupportedException($"User management is not supported for {connection.DatabaseType}.")
            };

            _logger.LogInformation("Created database user {User} on entity {EntityId} with {Access} access",
                username, entityId, request.AccessLevel);

            return new DatabaseUserOperationResult
            {
                Ok = true,
                Username = username,
                // Returned exactly once. Only ever surfaced when we generated it — an operator-supplied
                // password is already known to them and echoing it back would only widen its exposure.
                GeneratedPassword = generated ? password : null,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Creating database user on entity {EntityId} failed", entityId);
            return Fail(username, Truncate(ex.Message));
        }
    }

    private async Task<string> CreateSqlServerUserAsync(DatabaseConnection c, string username, string password, CreateDatabaseUserRequest request, CancellationToken ct)
    {
        var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.SQLServer, username);
        var pwd = ExternalConnectionFactory.EscapeLiteral(password);

        // Azure SQL Database (EngineEdition 5) has no server-level CREATE LOGIN from a user database — it
        // needs a contained user that carries its own password instead.
        var contained = await IsAzureSqlDatabaseAsync(c, ct);

        if (!contained && request.CreateServerLogin)
        {
            // CREATE LOGIN is server-scoped and must run against master.
            await using var master = ExternalConnectionFactory.CreateForCatalog(c, "master", readOnly: false);
            await master.OpenAsync(ct);
            await ExecuteAsync(master, ct, $"CREATE LOGIN {id} WITH PASSWORD = '{pwd}'");
        }

        await using var db = ExternalConnectionFactory.Create(c, readOnly: false);
        await db.OpenAsync(ct);

        await ExecuteAsync(db, ct, contained
            ? $"CREATE USER {id} WITH PASSWORD = '{pwd}'"
            : $"CREATE USER {id} FOR LOGIN {id}");

        foreach (var role in SqlServerRolesFor(request.AccessLevel))
            await ExecuteAsync(db, ct, $"ALTER ROLE {ExternalConnectionFactory.QuoteIdentifier(DataSourceType.SQLServer, role)} ADD MEMBER {id}");

        return contained
            ? $"Created contained database user '{username}' (Azure SQL Database — no server login)."
            : $"Created login and database user '{username}'.";
    }

    private static IEnumerable<string> SqlServerRolesFor(DatabaseAccessLevel level) => level switch
    {
        DatabaseAccessLevel.ReadOnly => new[] { "db_datareader" },
        DatabaseAccessLevel.ReadWrite => new[] { "db_datareader", "db_datawriter" },
        DatabaseAccessLevel.Owner => new[] { "db_owner" },
        _ => new[] { "db_datareader" }
    };

    private async Task<string> CreatePostgresUserAsync(DatabaseConnection c, string username, string password, DatabaseAccessLevel level, CancellationToken ct)
    {
        var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.PostgreSQL, username);
        var pwd = ExternalConnectionFactory.EscapeLiteral(password);
        var db = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.PostgreSQL, c.DatabaseName ?? string.Empty);

        await using var conn = ExternalConnectionFactory.Create(c, readOnly: false);
        await conn.OpenAsync(ct);

        await ExecuteAsync(conn, ct, $"CREATE ROLE {id} WITH LOGIN PASSWORD '{pwd}'");
        await ExecuteAsync(conn, ct, $"GRANT CONNECT ON DATABASE {db} TO {id}");
        await ExecuteAsync(conn, ct, $"GRANT USAGE ON SCHEMA public TO {id}");

        if (level == DatabaseAccessLevel.Owner)
        {
            await ExecuteAsync(conn, ct, $"GRANT ALL PRIVILEGES ON DATABASE {db} TO {id}");
            await ExecuteAsync(conn, ct, $"GRANT ALL ON SCHEMA public TO {id}");
            await ExecuteAsync(conn, ct, $"GRANT ALL ON ALL TABLES IN SCHEMA public TO {id}");
            await ExecuteAsync(conn, ct, $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO {id}");
        }
        else
        {
            var privileges = level == DatabaseAccessLevel.ReadWrite ? "SELECT, INSERT, UPDATE, DELETE" : "SELECT";
            await ExecuteAsync(conn, ct, $"GRANT {privileges} ON ALL TABLES IN SCHEMA public TO {id}");
            // Without this, tables created later are invisible to the new role.
            await ExecuteAsync(conn, ct, $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT {privileges} ON TABLES TO {id}");
            if (level == DatabaseAccessLevel.ReadWrite)
                await ExecuteAsync(conn, ct, $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {id}");
        }

        return $"Created role '{username}' with {DescribeLevel(level)} on schema public.";
    }

    private async Task<string> CreateMySqlUserAsync(DatabaseConnection c, string username, string host, string password, DatabaseAccessLevel level, CancellationToken ct)
    {
        // MySQL principals are string literals ('user'@'host'), not identifiers — the whole reason
        // ParsePrincipal validates both halves against a strict pattern before we get here.
        var user = ExternalConnectionFactory.EscapeLiteral(username);
        var hostLiteral = ExternalConnectionFactory.EscapeLiteral(host);
        var pwd = ExternalConnectionFactory.EscapeLiteral(password);
        var db = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.MySQL, c.DatabaseName ?? string.Empty);

        await using var conn = ExternalConnectionFactory.Create(c, readOnly: false);
        await conn.OpenAsync(ct);

        await ExecuteAsync(conn, ct, $"CREATE USER '{user}'@'{hostLiteral}' IDENTIFIED BY '{pwd}'");

        var privileges = level switch
        {
            DatabaseAccessLevel.ReadOnly => "SELECT",
            DatabaseAccessLevel.ReadWrite => "SELECT, INSERT, UPDATE, DELETE",
            DatabaseAccessLevel.Owner => "ALL PRIVILEGES",
            _ => "SELECT"
        };
        await ExecuteAsync(conn, ct, $"GRANT {privileges} ON {db}.* TO '{user}'@'{hostLiteral}'");

        return $"Created user '{username}'@'{host}' with {DescribeLevel(level)} on {c.DatabaseName}.";
    }

    private async Task<string> CreateClickHouseUserAsync(DatabaseConnection c, string username, string password, DatabaseAccessLevel level, CancellationToken ct)
    {
        var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.ClickHouse, username);
        var pwd = ExternalConnectionFactory.EscapeLiteral(password);
        var db = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.ClickHouse, c.DatabaseName ?? string.Empty);

        await QueryClickHouseAsync(c, $"CREATE USER {id} IDENTIFIED WITH sha256_password BY '{pwd}'", readOnly: false, ct);

        var privileges = level switch
        {
            DatabaseAccessLevel.ReadOnly => "SELECT",
            DatabaseAccessLevel.ReadWrite => "SELECT, INSERT, ALTER",
            DatabaseAccessLevel.Owner => "ALL",
            _ => "SELECT"
        };
        await QueryClickHouseAsync(c, $"GRANT {privileges} ON {db}.* TO {id}", readOnly: false, ct);

        return $"Created user '{username}' with {DescribeLevel(level)} on {c.DatabaseName}.";
    }

    // ---- Reset password ----

    public async Task<DatabaseUserOperationResult> ResetPasswordAsync(string entityId, string companyId, ResetDatabaseUserPasswordRequest request, CancellationToken ct = default)
    {
        var (connection, error) = await LoadAdminConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return Fail(request.Username, error!);

        var (username, host, nameError) = ParsePrincipal(request.Username, connection.DatabaseType);
        if (nameError != null) return Fail(request.Username, nameError);

        var password = request.Password;
        var generated = string.IsNullOrEmpty(password);
        if (generated) password = GeneratePassword();
        if (!IsUsablePasswordLiteral(password!))
            return Fail(username, "The password contains a character that cannot be used safely (quote, backslash or control character), or is not between 12 and 128 characters.");

        var pwd = ExternalConnectionFactory.EscapeLiteral(password!);

        try
        {
            switch (connection.DatabaseType)
            {
                case DataSourceType.SQLServer:
                {
                    var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.SQLServer, username);
                    if (await IsAzureSqlDatabaseAsync(connection, ct))
                    {
                        await using var db = ExternalConnectionFactory.Create(connection, readOnly: false);
                        await db.OpenAsync(ct);
                        await ExecuteAsync(db, ct, $"ALTER USER {id} WITH PASSWORD = '{pwd}'");
                    }
                    else
                    {
                        await using var master = ExternalConnectionFactory.CreateForCatalog(connection, "master", readOnly: false);
                        await master.OpenAsync(ct);
                        await ExecuteAsync(master, ct, $"ALTER LOGIN {id} WITH PASSWORD = '{pwd}'");
                    }
                    break;
                }
                case DataSourceType.PostgreSQL:
                {
                    await using var conn = ExternalConnectionFactory.Create(connection, readOnly: false);
                    await conn.OpenAsync(ct);
                    await ExecuteAsync(conn, ct,
                        $"ALTER ROLE {ExternalConnectionFactory.QuoteIdentifier(DataSourceType.PostgreSQL, username)} WITH PASSWORD '{pwd}'");
                    break;
                }
                case DataSourceType.MySQL:
                {
                    await using var conn = ExternalConnectionFactory.Create(connection, readOnly: false);
                    await conn.OpenAsync(ct);
                    await ExecuteAsync(conn, ct,
                        $"ALTER USER '{ExternalConnectionFactory.EscapeLiteral(username)}'@'{ExternalConnectionFactory.EscapeLiteral(host)}' IDENTIFIED BY '{pwd}'");
                    break;
                }
                case DataSourceType.ClickHouse:
                    await QueryClickHouseAsync(connection,
                        $"ALTER USER {ExternalConnectionFactory.QuoteIdentifier(DataSourceType.ClickHouse, username)} IDENTIFIED WITH sha256_password BY '{pwd}'",
                        readOnly: false, ct);
                    break;
                case DataSourceType.DuckDB:
                    return Fail(username, DuckDbUnsupported);
                default:
                    return Fail(username, $"User management is not supported for {connection.DatabaseType}.");
            }

            _logger.LogInformation("Reset password for database user {User} on entity {EntityId}", username, entityId);

            return new DatabaseUserOperationResult
            {
                Ok = true,
                Username = username,
                GeneratedPassword = generated ? password : null,
                Message = $"Password reset for '{username}'."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resetting password on entity {EntityId} failed", entityId);
            return Fail(username, Truncate(ex.Message));
        }
    }

    // ---- Drop user ----

    public async Task<DatabaseUserOperationResult> DropUserAsync(string entityId, string companyId, DropDatabaseUserRequest request, CancellationToken ct = default)
    {
        var (connection, error) = await LoadAdminConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return Fail(request.Username, error!);

        var (username, host, nameError) = ParsePrincipal(request.Username, connection.DatabaseType);
        if (nameError != null) return Fail(request.Username, nameError);

        try
        {
            switch (connection.DatabaseType)
            {
                case DataSourceType.SQLServer:
                {
                    var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.SQLServer, username);
                    await using (var db = ExternalConnectionFactory.Create(connection, readOnly: false))
                    {
                        await db.OpenAsync(ct);
                        await ExecuteAsync(db, ct, $"DROP USER {id}");
                    }

                    if (request.DropServerLogin && !await IsAzureSqlDatabaseAsync(connection, ct))
                    {
                        await using var master = ExternalConnectionFactory.CreateForCatalog(connection, "master", readOnly: false);
                        await master.OpenAsync(ct);
                        await ExecuteAsync(master, ct, $"DROP LOGIN {id}");
                    }
                    break;
                }
                case DataSourceType.PostgreSQL:
                {
                    var id = ExternalConnectionFactory.QuoteIdentifier(DataSourceType.PostgreSQL, username);
                    await using var conn = ExternalConnectionFactory.Create(connection, readOnly: false);
                    await conn.OpenAsync(ct);
                    // Postgres refuses to drop a role that still holds privileges or owns objects in this
                    // database. DROP OWNED clears both — for a provisioned read/write account that means its
                    // grants; if the role owns tables, those go with it.
                    await ExecuteAsync(conn, ct, $"DROP OWNED BY {id}");
                    await ExecuteAsync(conn, ct, $"DROP ROLE {id}");
                    break;
                }
                case DataSourceType.MySQL:
                {
                    await using var conn = ExternalConnectionFactory.Create(connection, readOnly: false);
                    await conn.OpenAsync(ct);
                    await ExecuteAsync(conn, ct,
                        $"DROP USER '{ExternalConnectionFactory.EscapeLiteral(username)}'@'{ExternalConnectionFactory.EscapeLiteral(host)}'");
                    break;
                }
                case DataSourceType.ClickHouse:
                    await QueryClickHouseAsync(connection,
                        $"DROP USER {ExternalConnectionFactory.QuoteIdentifier(DataSourceType.ClickHouse, username)}",
                        readOnly: false, ct);
                    break;
                case DataSourceType.DuckDB:
                    return Fail(username, DuckDbUnsupported);
                default:
                    return Fail(username, $"User management is not supported for {connection.DatabaseType}.");
            }

            _logger.LogInformation("Dropped database user {User} on entity {EntityId}", username, entityId);
            return new DatabaseUserOperationResult { Ok = true, Username = username, Message = $"Dropped '{username}'." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dropping database user on entity {EntityId} failed", entityId);
            return Fail(username, Truncate(ex.Message));
        }
    }

    // ---- Credentials ----

    /// <summary>Loads the entity's ordinary connection with the secret decrypted in place (the field keeps its
    /// <c>SecretEncrypted</c> name but now holds plaintext — same contract as <c>DatabaseTableService</c>).</summary>
    private async Task<DatabaseConnection?> LoadDecryptedConnectionAsync(string entityId, string companyId, CancellationToken ct)
    {
        var connection = await _context.DatabaseConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EntityId == entityId && c.CompanyId == companyId, ct);
        if (connection == null) return null;

        if (!string.IsNullOrEmpty(connection.SecretEncrypted))
            connection.SecretEncrypted = _protector.Decrypt(connection.SecretEncrypted);

        return connection;
    }

    /// <summary>
    /// The same connection details but authenticating as the stored admin credential. Returns an error rather
    /// than falling back to the least-privilege account — silently escalating the read-only credential is
    /// exactly the behaviour the separate credential exists to prevent.
    /// </summary>
    private async Task<(DatabaseConnection? Connection, string? Error)> LoadAdminConnectionAsync(string entityId, string companyId, CancellationToken ct)
    {
        var connection = await LoadDecryptedConnectionAsync(entityId, companyId, ct);
        if (connection == null)
            return (null, "No connection is configured for this database.");

        var credential = await _context.DatabaseAdminCredentials.AsNoTracking()
            .FirstOrDefaultAsync(a => a.EntityId == entityId && a.CompanyId == companyId, ct);
        if (credential == null || string.IsNullOrEmpty(credential.SecretEncrypted))
            return (null, "No admin credential is configured for this database. Save one before managing users.");

        return (new DatabaseConnection
        {
            EntityId = connection.EntityId,
            DatabaseType = connection.DatabaseType,
            Host = connection.Host,
            Port = connection.Port,
            DatabaseName = connection.DatabaseName,
            UseSsl = connection.UseSsl,
            FilePath = connection.FilePath,
            Username = credential.Username,
            SecretEncrypted = _protector.Decrypt(credential.SecretEncrypted)
        }, null);
    }

    // ---- Validation and generation ----

    private const string DuckDbUnsupported =
        "DuckDB is an embedded, file-based engine — it has no user accounts, logins or roles.";

    /// <summary>
    /// Splits and validates a principal name. MySQL principals are <c>user@host</c>, so both halves are checked;
    /// every other engine takes the whole string as one identifier. Returns the error message when invalid.
    /// </summary>
    private static (string Username, string Host, string? Error) ParsePrincipal(string raw, DataSourceType type)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
            return (string.Empty, "%", "A username is required.");

        if (type == DataSourceType.MySQL)
        {
            var at = value.LastIndexOf('@');
            var user = at < 0 ? value : value[..at];
            var host = at < 0 ? "%" : value[(at + 1)..];

            if (!UsernamePattern.IsMatch(user))
                return (user, host, InvalidNameMessage);
            if (!MySqlHostPattern.IsMatch(host))
                return (user, host, "The host part of the username contains characters that are not allowed.");
            return (user, host, null);
        }

        return UsernamePattern.IsMatch(value)
            ? (value, "%", null)
            : (value, "%", InvalidNameMessage);
    }

    private const string InvalidNameMessage =
        "The username must start with a letter, digit or underscore, be at most 63 characters, and contain only letters, digits and _ $ . -";

    /// <summary>
    /// Rejects passwords that can't be embedded safely in a SQL string literal. Quotes are technically
    /// escapable but excluded anyway — no engine here accepts a parameter in the DDL that sets a password,
    /// so keeping the character set narrow is the cheapest way to guarantee the statement stays well-formed.
    /// </summary>
    private static bool IsUsablePasswordLiteral(string password)
    {
        if (password.Length is < 12 or > 128) return false;
        return !password.Any(ch => ch is '\'' or '"' or '\\' or '`' || char.IsControl(ch));
    }

    /// <summary>Cryptographically random password from an alphabet with no quoting hazards or lookalike glyphs.</summary>
    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#%*+=?";
        var chars = new char[24];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    private static string DescribeLevel(DatabaseAccessLevel level) => level switch
    {
        DatabaseAccessLevel.ReadOnly => "read-only access",
        DatabaseAccessLevel.ReadWrite => "read/write access",
        DatabaseAccessLevel.Owner => "full ownership",
        _ => "read-only access"
    };

    // ---- Execution helpers ----

    private static async Task ExecuteAsync(DbConnection connection, CancellationToken ct, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = AdminCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<object?[]>> ReadRowsAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        var rows = new List<object?[]>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = AdminCommandTimeoutSeconds;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>True for Azure SQL Database, which has no server-level login management from a user database.</summary>
    private static async Task<bool> IsAzureSqlDatabaseAsync(DatabaseConnection c, CancellationToken ct)
    {
        try
        {
            await using var conn = ExternalConnectionFactory.Create(c, readOnly: true);
            await conn.OpenAsync(ct);
            await using var command = conn.CreateCommand();
            command.CommandText = "SELECT CAST(SERVERPROPERTY('EngineEdition') AS INT)";
            command.CommandTimeout = AdminCommandTimeoutSeconds;
            var result = await command.ExecuteScalarAsync(ct);
            return ToLong(result) == 5; // 5 = Azure SQL Database
        }
        catch
        {
            // If we can't tell, assume a normal SQL Server and let the real statement report the failure.
            return false;
        }
    }

    /// <summary>
    /// Runs a ClickHouse statement over HTTP. ClickHouse has no ADO driver, so admin DDL goes the same route
    /// as reads — but without <c>?readonly=1</c>, which would reject it.
    /// </summary>
    private async Task<string> QueryClickHouseAsync(DatabaseConnection c, string query, bool readOnly, CancellationToken ct)
    {
        var protocol = c.UseSsl ? "https" : "http";
        var url = readOnly ? $"{protocol}://{c.Host}:{c.Port}/?readonly=1" : $"{protocol}://{c.Host}:{c.Port}/";

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (!string.IsNullOrEmpty(c.Username))
        {
            // SecretEncrypted holds the decrypted password by this point (see LoadDecryptedConnectionAsync).
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Username}:{c.SecretEncrypted}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ClickHouse request failed ({(int)response.StatusCode}): {body}");
        return body;
    }

    private static List<Dictionary<string, JsonElement>> ParseJsonEachRow(string body)
    {
        var rows = new List<Dictionary<string, JsonElement>>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var row = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
                if (row != null) rows.Add(row);
            }
            catch
            {
                // Skip malformed lines, as the table-discovery path does.
            }
        }
        return rows;
    }

    private static object? JsonValue(Dictionary<string, JsonElement> row, string key) =>
        row.TryGetValue(key, out var element)
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.Null => null,
                _ => element.ToString()
            }
            : null;

    // ---- Conversions ----

    private static long? ToLong(object? value) => value switch
    {
        null or DBNull => null,
        long l => l,
        int i => i,
        decimal d => (long)d,
        double d => (long)d,
        float f => (long)f,
        ulong u => (long)u,
        _ => long.TryParse(value.ToString(), out var parsed) ? parsed : null
    };

    private static string ToStr(object? value) => value switch
    {
        null or DBNull => string.Empty,
        string s => s,
        _ => value.ToString() ?? string.Empty
    };

    private static List<string> SplitCsv(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static DatabaseUserOperationResult Fail(string username, string error) =>
        new() { Ok = false, Username = username, Error = error };

    private static string Truncate(string s) => string.IsNullOrEmpty(s) || s.Length <= 300 ? s : s[..300];

    private static DatabaseAdminCredentialDto ToDto(DatabaseAdminCredential a) => new()
    {
        EntityId = a.EntityId,
        Username = a.Username,
        HasSecret = !string.IsNullOrEmpty(a.SecretEncrypted)
    };
}

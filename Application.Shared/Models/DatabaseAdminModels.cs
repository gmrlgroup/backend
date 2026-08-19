using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// What a given engine can actually do. Returned per entity so the UI greys out unsupported actions
/// with a reason instead of hard-coding the engine matrix in Razor. DuckDB is the interesting case:
/// it's an embedded single-file engine with no user accounts at all, so only the size check applies.
/// </summary>
public class DatabaseAdminCapabilities
{
    public bool CanCheckSize { get; set; }
    public bool CanListUsers { get; set; }
    public bool CanCreateUser { get; set; }
    public bool CanDropUser { get; set; }
    public bool CanResetPassword { get; set; }

    /// <summary>Why user management is unavailable, when it is. Null when everything is supported.</summary>
    public string? UnsupportedReason { get; set; }
}

/// <summary>A Database-type entity as listed on the Database Management page.</summary>
public class DatabaseAdminEntityDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DataSourceType DatabaseType { get; set; }
    public string? Host { get; set; }
    public string? DatabaseName { get; set; }
    public string? FilePath { get; set; }

    /// <summary>False when the entity has no <see cref="DatabaseConnection"/> configured yet.</summary>
    public bool HasConnection { get; set; }

    /// <summary>True when an elevated credential is stored — required for every user operation.</summary>
    public bool HasAdminCredential { get; set; }

    public DatabaseAdminCapabilities Capabilities { get; set; } = new();
}

/// <summary>Stored admin credential, safe to send to the browser (no secret).</summary>
public class DatabaseAdminCredentialDto
{
    public string EntityId { get; set; } = string.Empty;
    public string? Username { get; set; }

    /// <summary>True when a password is stored; the secret itself is never returned.</summary>
    public bool HasSecret { get; set; }
}

/// <summary>Payload to create/update the admin credential. Blank <see cref="Secret"/> keeps the existing password.</summary>
public class DatabaseAdminCredentialRequest
{
    public string? Username { get; set; }
    public string? Secret { get; set; }
}

/// <summary>Space used by one table (or the engine's nearest equivalent).</summary>
public class TableSizeDto
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FullName => string.IsNullOrEmpty(Schema) ? Name : $"{Schema}.{Name}";
    public long? RowCount { get; set; }
    public long TotalBytes { get; set; }
}

/// <summary>Space usage for a database. Log/free are SQL Server concepts and stay null elsewhere.</summary>
public class DatabaseSizeDto
{
    public long? TotalBytes { get; set; }
    public long? DataBytes { get; set; }
    public long? LogBytes { get; set; }
    public long? FreeBytes { get; set; }
    public List<TableSizeDto> Tables { get; set; } = new();

    /// <summary>Set when the size couldn't be read. Null on success.</summary>
    public string? Error { get; set; }
}

/// <summary>A principal that exists on the database.</summary>
public class DatabaseUserDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Engine-specific principal type, e.g. "SQL_USER", "ROLE", "sha256_password".</summary>
    public string? Type { get; set; }

    /// <summary>True when the account exists but cannot authenticate.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Roles/grants held, as reported by the engine.</summary>
    public List<string> Roles { get; set; } = new();
}

/// <summary>The users on a database, or an error if listing failed.</summary>
public class DatabaseUserListDto
{
    public List<DatabaseUserDto> Users { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>Request to provision a user. A null <see cref="Password"/> asks the server to generate a strong one.</summary>
public class CreateDatabaseUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public DatabaseAccessLevel AccessLevel { get; set; } = DatabaseAccessLevel.ReadOnly;

    /// <summary>SQL Server only: also create the server-level login. Off means "the login already exists".</summary>
    public bool CreateServerLogin { get; set; } = true;
}

/// <summary>Request to set a new password on an existing user.</summary>
public class ResetDatabaseUserPasswordRequest
{
    public string Username { get; set; } = string.Empty;

    /// <summary>Null asks the server to generate a strong password.</summary>
    public string? Password { get; set; }
}

/// <summary>Request to remove a user.</summary>
public class DropDatabaseUserRequest
{
    public string Username { get; set; } = string.Empty;

    /// <summary>SQL Server only: also drop the server-level login, not just the database user.</summary>
    public bool DropServerLogin { get; set; } = true;
}

/// <summary>
/// Outcome of a user operation. <see cref="GeneratedPassword"/> is returned exactly once, on the
/// response to the call that set it — it is never stored and never written to the activity log.
/// </summary>
public class DatabaseUserOperationResult
{
    public bool Ok { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? GeneratedPassword { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

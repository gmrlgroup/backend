using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>Connection details for a Database entity, safe to send to the browser (no secret).</summary>
public class DatabaseConnectionDto
{
    public string Id { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DataSourceType DatabaseType { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public bool UseSsl { get; set; }
    public string? FilePath { get; set; }
    /// <summary>True when a password is stored; the secret itself is never returned.</summary>
    public bool HasSecret { get; set; }
}

/// <summary>Payload to create/update a Database entity's connection. Blank <see cref="Secret"/> keeps the existing password.</summary>
public class DatabaseConnectionRequest
{
    public DataSourceType DatabaseType { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public string? Secret { get; set; }
    public bool UseSsl { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>Result of testing a database connection.</summary>
public class DatabaseConnectionTestResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}

/// <summary>A table discovered in the database, with any existing Table-entity match.</summary>
public class DatabaseTableInfoDto
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>"{schema}.{name}" — used as the Table entity name.</summary>
    public string FullName { get; set; } = string.Empty;
    public string? ExistingEntityId { get; set; }
}

/// <summary>The tables the database exposes, or an error if listing failed.</summary>
public class DatabaseTableDiscoveryDto
{
    public List<DatabaseTableInfoDto> Tables { get; set; } = new();
    /// <summary>Set when the tables couldn't be listed (connection/query failure). Null on success.</summary>
    public string? Error { get; set; }
}

/// <summary>User's chosen tables (by "{schema}.{name}") to materialize as Table entities.</summary>
public class DatabaseTableCommitRequest
{
    public List<string> Tables { get; set; } = new();
}

/// <summary>Summary of what a table commit created or updated.</summary>
public class DatabaseTableCommitResult
{
    public int TablesAdded { get; set; }
    public int TablesUpdated { get; set; }
    public int DependenciesCreated { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Freshness-check configuration for a Table entity, safe to send to the browser.</summary>
public class TableCheckDto
{
    public string EntityId { get; set; } = string.Empty;
    public string? FreshnessColumn { get; set; }
    public int MaxAgeMinutes { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>True when the Table entity has a parent Database entity with a saved connection — required to run the check.</summary>
    public bool HasDatabaseConnection { get; set; }
}

/// <summary>Payload to create/update a Table entity's freshness check.</summary>
public class TableCheckRequest
{
    public string? FreshnessColumn { get; set; }
    public int MaxAgeMinutes { get; set; }
    public bool IsEnabled { get; set; }
}

/// <summary>Outcome of probing a database connection (SELECT 1).</summary>
public class DatabaseProbeResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public double? ResponseMs { get; set; }
}

/// <summary>Outcome of a table-freshness query (MAX of the timestamp column + row count).</summary>
public class TableFreshnessResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public DateTime? LastUpdatedUtc { get; set; }
    public long? RowCount { get; set; }
    public double? ResponseMs { get; set; }
    /// <summary>Age of the newest row in minutes (null when no timestamp could be read).</summary>
    public double? AgeMinutes { get; set; }
    public bool IsStale { get; set; }
}

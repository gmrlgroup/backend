using Application.Shared.Models;

namespace Application.Shared.Services;

/// <summary>
/// Manages a Database-type entity's connection details and enumerates its tables,
/// materializing the chosen ones as Table entities with a dependency on the database.
/// </summary>
public interface IDatabaseTableService
{
    Task<DatabaseConnectionDto?> GetConnectionAsync(string entityId, string companyId, CancellationToken ct = default);
    Task<DatabaseConnectionDto> SaveConnectionAsync(string entityId, string companyId, DatabaseConnectionRequest request, string? modifiedBy, CancellationToken ct = default);
    Task<bool> DeleteConnectionAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Opens the configured connection and runs a trivial query; returns ok + any error message.</summary>
    Task<DatabaseConnectionTestResult> TestConnectionAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Lists the database's tables as {schema}.{name}, matched to existing Table entities. Sets Error (no throw) on connection failure.</summary>
    Task<DatabaseTableDiscoveryDto> DiscoverTablesAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Creates/updates Table entities for the chosen tables and wires each Table → Database dependency.</summary>
    Task<DatabaseTableCommitResult> CommitTablesAsync(string entityId, string companyId, DatabaseTableCommitRequest request, string? modifiedBy, CancellationToken ct = default);

    // ---- Table freshness checks ----

    /// <summary>Gets a Table entity's freshness-check config (null when none configured).</summary>
    Task<TableCheckDto?> GetTableCheckAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Creates/updates the freshness-check config for a Table entity.</summary>
    Task<TableCheckDto> SaveTableCheckAsync(string entityId, string companyId, TableCheckRequest request, string? modifiedBy, CancellationToken ct = default);

    /// <summary>Removes a Table entity's freshness-check config.</summary>
    Task<bool> DeleteTableCheckAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Resolves the Table's parent Database connection and runs its freshness query now (for the UI "run now").</summary>
    Task<TableFreshnessResult> RunFreshnessCheckAsync(string entityId, string companyId, CancellationToken ct = default);

    // ---- Pure probes (no DbContext; safe to call concurrently from the ping job) ----

    /// <summary>Opens the (already-decrypted) connection read-only and runs SELECT 1, timing the round-trip.</summary>
    Task<DatabaseProbeResult> ProbeConnectionAsync(DatabaseConnection decryptedConnection, CancellationToken ct = default);

    /// <summary>Reads MAX(timestamp) + row count (read-only) for a table over the (already-decrypted) connection.</summary>
    Task<TableFreshnessResult> CheckFreshnessAsync(DatabaseConnection decryptedConnection, string tableFullName, string freshnessColumn, int maxAgeMinutes, CancellationToken ct = default);

    /// <summary>Loads and decrypts the connection of the Database entity a given Table entity depends on (null when none).</summary>
    Task<DatabaseConnection?> GetDecryptedParentConnectionAsync(string tableEntityId, string companyId, CancellationToken ct = default);

    /// <summary>Loads and decrypts a Database entity's own connection (null when none).</summary>
    Task<DatabaseConnection?> GetDecryptedConnectionAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Runs a read-only SELECT over the (already-decrypted) connection and streams the result
    /// to a UTF-8 CSV file (header + rows). Returns the number of data rows written. Used by scheduled
    /// ingestion to pull an external table/query into a dataset.</summary>
    Task<int> ReadToTempCsvAsync(DatabaseConnection decryptedConnection, string query, string destCsvPath, CancellationToken ct = default);
}

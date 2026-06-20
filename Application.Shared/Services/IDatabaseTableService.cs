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
}

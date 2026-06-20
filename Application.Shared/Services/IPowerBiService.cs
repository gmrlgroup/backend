using Application.Shared.Models;

namespace Application.Shared.Services;

/// <summary>
/// Reads refresh history and triggers refreshes for Dataset entities backed by Power BI,
/// plus manages the entity's link to a <see cref="PowerBiConnection"/>.
/// </summary>
public interface IPowerBiService
{
    Task<PowerBiDatasetLinkDto?> GetLinkAsync(string entityId, string companyId);
    Task<PowerBiDatasetLinkDto> SaveLinkAsync(string entityId, string companyId, PowerBiDatasetLinkRequest request, string? modifiedBy);
    Task<bool> DeleteLinkAsync(string entityId, string companyId);

    Task<List<PowerBiRefreshDto>> GetRefreshHistoryAsync(string entityId, string companyId, int top = 20, CancellationToken ct = default);
    Task<PowerBiActionResult> TriggerRefreshAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Reads the dataset's scheduled-refresh config and computes the next run. Null when the dataset has no schedule (e.g. DirectQuery).</summary>
    Task<PowerBiRefreshScheduleDto?> GetRefreshScheduleAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Discovers the databases (data sources) and tables the dataset draws from, with existing-entity matches.</summary>
    Task<PowerBiDiscoveryDto> GetDiscoveryAsync(string entityId, string companyId, CancellationToken ct = default);

    /// <summary>Creates/updates Database + Table entities for the chosen items and wires the full-lineage dependency edges.</summary>
    Task<PowerBiLineageCommitResult> CommitLineageAsync(string entityId, string companyId, PowerBiLineageCommitRequest request, string? modifiedBy, CancellationToken ct = default);
}

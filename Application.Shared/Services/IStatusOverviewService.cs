using Application.Shared.Models;

namespace Application.Shared.Services;

/// <summary>
/// Read-only aggregation over <see cref="AssetStatusHistory"/> for the public status board:
/// monitored assets grouped by type with a per-day status timeline, plus a single day's raw events.
/// </summary>
public interface IStatusOverviewService
{
    /// <summary>Builds the board for a company: active entities grouped by type, each with the last status of every day in the window.</summary>
    Task<StatusOverviewDto> GetOverviewAsync(string companyId, int days = 30, CancellationToken ct = default);

    /// <summary>Returns the status-history events recorded for one entity on a single UTC day, ordered by time.</summary>
    Task<List<StatusDayEventDto>> GetDayEventsAsync(string companyId, string entityId, DateTime dateUtc, CancellationToken ct = default);
}

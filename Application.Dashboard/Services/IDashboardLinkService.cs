using Application.Shared.Models.Dashboards;

namespace Application.Dashboard.Services;

public interface IDashboardLinkService
{
    /// <summary>The table linked to a dashboard page for a company, or null if none.</summary>
    Task<DashboardDataLink?> GetAsync(string companyId, string pageUrl, CancellationToken ct = default);

    /// <summary>All dashboard pages a given table is currently linked to (for the tables page UI).</summary>
    Task<List<DashboardDataLink>> GetForTableAsync(string companyId, string datasetId, string tableName, CancellationToken ct = default);

    /// <summary>Connect a table to a dashboard page (upsert — one link per company+page).</summary>
    Task<DashboardDataLink> SetAsync(string companyId, string pageUrl, string datasetId, string tableName, string? userId, CancellationToken ct = default);

    /// <summary>Remove the link for a dashboard page.</summary>
    Task<bool> DeleteAsync(string companyId, string pageUrl, CancellationToken ct = default);
}

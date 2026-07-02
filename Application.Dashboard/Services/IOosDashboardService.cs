using Application.Shared.Models.Dashboards.Oos;

namespace Application.Dashboard.Services;

public interface IOosDashboardService
{
    /// <summary>
    /// Builds the Out-of-Stock dashboard dataset for a company at a given "as of" date from the
    /// pre-computed itemized table in the linked DuckDB dataset.
    /// </summary>
    Task<OosDashboardResponse> GetAsync(string companyId, DateTime asOf, int limit, CancellationToken ct = default);
}

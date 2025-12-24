using Application.Shared.Data;
using Application.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Shared.Services.Data;

public class SalesDashboardService : ISalesDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SalesDashboardService> _logger;

    public SalesDashboardService(
        ApplicationDbContext context,
        ILogger<SalesDashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SalesKpiData> GetKpiDataAsync(string companyId, string userId)
    {
        try
        {
            var latestData = await GetLatestDataByStoreAndSchemeAsync(companyId, userId);
            
            return new SalesKpiData
            {
                TotalSales = latestData.Sum(d => d.TotalSales),
                TotalTransactions = latestData.Sum(d => d.TotalTransactions),
                TotalStores = latestData.Select(d => d.StoreCode).Distinct().Count(),
                TotalSchemes = latestData.Select(d => d.Scheme).Distinct().Count(),
                LastUpdated = latestData.Any() ? latestData.Max(d => d.LastUpdated) : DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KPI data for company: {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<SalesBannerKpi>> GetBannerKpiDataAsync(string companyId, string userId)
    {
        try
        {
            var latestData = await GetLatestDataByStoreAndSchemeAsync(companyId, userId);
            
            return latestData
                .GroupBy(d => d.Scheme)
                .Select(g => new SalesBannerKpi
                {
                    Banner = g.Key,
                    TotalSales = g.Sum(d => d.TotalSales),
                    TotalTransactions = g.Sum(d => d.TotalTransactions),
                    StoreCount = g.Select(d => d.StoreCode).Distinct().Count(),
                    LastUpdated = g.Max(d => d.LastUpdated)
                })
                .OrderByDescending(b => b.TotalSales)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting banner KPI data for company: {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<SalesDashboardData>> GetDashboardDataAsync(string companyId, string userId)
    {
        try
        {
            return await GetLatestDataByStoreAndSchemeAsync(companyId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data for company: {CompanyId}", companyId);
            throw;
        }
    }

    public async Task<List<SalesDashboardData>> GetLatestDataByStoreAndSchemeAsync(string companyId, string userId, bool includeCategory = false)
    {
        try
        {

            var latestPerKey = _context.SalesData
                .Where(sd => sd.CompanyId == companyId && sd.ReceivedAt.Date == DateTime.Now.Date)
                .GroupBy(sd => new { sd.StoreCode, sd.Scheme, sd.DivisionName, sd.CategoryName })
                .Select(g => new
                {
                    g.Key.StoreCode,
                    g.Key.Scheme,
                    g.Key.DivisionName,
                    g.Key.CategoryName,
                    ReceivedAt = g.Max(x => x.ReceivedAt)   // latest per (Store, Scheme)
                });

                var dashboardData = await _context.SalesData
                    .Where(sd => sd.CompanyId == companyId)
                    .Join(
                        latestPerKey,
                        sd => new { sd.StoreCode, sd.Scheme, sd.DivisionName, sd.CategoryName, sd.ReceivedAt },
                        l => new { l.StoreCode, l.Scheme, l.DivisionName, l.CategoryName, l.ReceivedAt },
                        (sd, l) => new SalesDashboardData
                        {
                            Scheme = sd.Scheme,
                            StoreCode = sd.StoreCode,
                            DivisionName = sd.DivisionName,
                            CategoryName = sd.CategoryName,
                            TotalSales = sd.NetAmountAcy,
                            TotalTransactions = sd.TotalTransactions,
                            TotalStoreTransactions = sd.TotalStoreTransactions.Value,
                            LastUpdated = sd.ReceivedAt
                        })
                    .ToListAsync();


            return dashboardData
                .OrderByDescending(d => d.TotalSales)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest data by store and scheme for company: {CompanyId}", companyId);
            throw;
        }
    }
}

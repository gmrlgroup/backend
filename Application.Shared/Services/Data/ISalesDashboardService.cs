using Application.Shared.Models.Data;

namespace Application.Shared.Services.Data;

public class SalesDashboardData
{
    public string? Scheme { get; set; }
    public string? StoreCode { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageBasket => TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;
    public DateTime LastUpdated { get; set; }
}

public class SalesKpiData
{
    public decimal TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageBasket => TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;
    public int TotalStores { get; set; }
    public int TotalSchemes { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class SalesBannerKpi
{
    public string? Banner { get; set; }
    public decimal TotalSales { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageBasket => TotalTransactions > 0 ? TotalSales / TotalTransactions : 0;
    public int StoreCount { get; set; }
    public DateTime LastUpdated { get; set; }
}

public interface ISalesDashboardService
{
    Task<SalesKpiData> GetKpiDataAsync(string companyId, string userId);
    Task<List<SalesBannerKpi>> GetBannerKpiDataAsync(string companyId, string userId);
    Task<List<SalesDashboardData>> GetDashboardDataAsync(string companyId, string userId);
    Task<List<SalesDashboardData>> GetLatestDataByStoreAndSchemeAsync(string companyId, string userId);
}

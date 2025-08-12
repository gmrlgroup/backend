using Application.Shared.Models.Data;

namespace Application.Shared.Services.Data;

public interface IRealTimeDataService
{
    // Sales Data Operations
    Task<SalesData?> CreateSalesDataAsync(SalesData salesData);
    Task<List<SalesData>> GetSalesDataAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);
    Task<List<SalesData>> GetSalesDataByStoreAsync(string companyId, string storeCode, string userId, int? fromHour = null, int? toHour = null);
    Task<List<SalesData>> GetSalesDataBySchemeAsync(string companyId, string scheme, string userId, int? fromHour = null, int? toHour = null);
    Task<SalesData?> GetSalesDataByIdAsync(string id, string companyId, string userId);
    Task<bool> MarkSalesDataAsProcessedAsync(string id, string userId);
    Task<bool> DeleteSalesDataAsync(string id, string userId);

    // Real-time Streaming Operations
    Task BroadcastSalesDataAsync(SalesData salesData, string companyId);
    Task<List<SalesData>> GetUnprocessedSalesDataAsync(string companyId, string userId);
    Task<int> GetSalesDataCountAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);

    // Aggregation Operations
    Task<decimal> GetTotalNetAmountAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);
    Task<int> GetTotalTransactionsAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);
    Task<Dictionary<string, decimal>> GetSalesByStoreAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);
    Task<Dictionary<string, decimal>> GetSalesBySchemeAsync(string companyId, string userId, int? fromHour = null, int? toHour = null);
}

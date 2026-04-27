using Application.Shared.Models;
using Application.Shared.Enums;

namespace Application.Shared.Services;

public interface IAssetStatusHistoryService
{
    Task<List<AssetStatusHistory>> GetEntityStatusHistoryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<AssetStatusHistory?> GetEntityStatusHistoryByIdAsync(int id);
    Task<AssetStatusHistory?> GetLatestEntityStatusAsync(string entityId);
    Task<List<AssetStatusHistory>> GetEntityStatusHistoryByStatusAsync(string entityId, AssetStatus status);
    Task<AssetStatusHistory> CreateEntityStatusHistoryAsync(AssetStatusHistory statusHistory);
    Task<AssetStatusHistory> UpdateEntityStatusHistoryAsync(AssetStatusHistory statusHistory);
    Task<bool> DeleteEntityStatusHistoryAsync(int id);
    Task<List<AssetStatusHistory>> GetEntityStatusHistoryByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<List<AssetStatusHistory>> GetAllEntityStatusHistoryAsync(string companyId);
    Task<Dictionary<AssetStatus, int>> GetEntityStatusSummaryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<double> GetAverageResponseTimeAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<double> GetAverageUptimeAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<AssetStatusHistory>> GetEntityStatusHistoryWithPaginationAsync(string entityId, int page, int pageSize);
    Task<int> GetEntityStatusHistoryCountAsync(string entityId);
    Task<bool> EntityStatusHistoryExistsAsync(int id);
}

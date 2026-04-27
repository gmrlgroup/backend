using Application.Shared.Models;
using Application.Shared.Enums;

namespace Application.Shared.Services;

public interface IMonitoredAssetService
{
    Task<List<MonitoredAsset>> GetEntitiesAsync(string companyId);
    Task<MonitoredAsset?> GetEntityAsync(string id);
    Task<MonitoredAsset> CreateEntityAsync(MonitoredAsset entity);
    Task<MonitoredAsset> UpdateEntityAsync(MonitoredAsset entity);
    Task<bool> DeleteEntityAsync(string id);
    Task<List<MonitoredAsset>> GetEntitiesByTypeAsync(string companyId, AssetType entityType);
    Task<List<MonitoredAsset>> GetCriticalEntitiesAsync(string companyId);
    Task<List<MonitoredAsset>> GetActiveEntitiesAsync(string companyId);
    Task<List<MonitoredAsset>> GetEntitiesWithCurrentStatusAsync(string companyId, AssetStatus status);
    Task<AssetStatusHistory> AddEntityStatusAsync(string entityId, AssetStatus status, string? statusMessage = null, double? responseTime = null, double? uptimePercentage = null);
    Task<AssetStatusHistory> UpdateEntityStatusWithIncidentHandlingAsync(string entityId, AssetStatus newStatus, string statusMessage, AssetStatus previousStatus, string companyId, string updatedBy);
    Task<AssetStatusHistory?> GetLatestEntityStatusAsync(string entityId);
    Task<List<AssetStatusHistory>> GetEntityStatusHistoryAsync(string entityId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<bool> UpdateEntityUptimeAsync(string id, double uptimePercentage);
    Task<bool> UpdateEntityResponseTimeAsync(string id, double responseTime);
    Task<bool> PingEntityAsync(string id);
    Task<List<MonitoredAsset>> SearchEntitiesAsync(string companyId, string searchTerm);
    Task<Dictionary<AssetStatus, int>> GetEntityStatusSummaryAsync(string companyId);
    Task<List<MonitoredAsset>> GetEntitiesDueForCheckAsync();
    Task<bool> EntityExistsAsync(string id);
    Task<List<MonitoredAsset>> GetEntitiesWithLatestStatusAsync(string companyId);
    Task<AssetDependency> CreateEntityDependencyAsync(AssetDependency dependency);
    Task<AssetDependency> UpdateEntityDependencyAsync(AssetDependency dependency);
    Task<bool> DeleteEntityDependencyAsync(string dependencyId);
    Task<List<AssetDependency>> GetEntityDependenciesAsync(string entityId);
    Task<List<AssetDependency>> GetEntityDependentsAsync(string entityId);
    Task<AssetDependencyTree> GetEntityDependencyTreeAsync(string entityId);
}

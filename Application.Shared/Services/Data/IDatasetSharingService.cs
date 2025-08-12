using Application.Shared.Models;

namespace Application.Shared.Services.Data;

public interface IDatasetSharingService
{
    Task<List<DatasetUserDto>> GetDatasetUsersAsync(string datasetId);
    Task<bool> ShareDatasetAsync(ShareDatasetRequest request, string sharedByUserId);
    Task<bool> UpdateDatasetUserTypeAsync(string datasetId, string userId, DatasetUserType userType);
    Task<bool> RemoveDatasetUserAsync(string datasetId, string userId);
    Task<List<Dataset>> GetSharedDatasetsAsync(string userId, string companyId);
    Task<bool> HasDatasetAccessAsync(string datasetId, string userId, DatasetUserType? requiredType = null);
}

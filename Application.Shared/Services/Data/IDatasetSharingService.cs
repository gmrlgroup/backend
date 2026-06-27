using Application.Shared.Models;

namespace Application.Shared.Services.Data;

public interface IDatasetSharingService
{
    Task<List<DatasetUserDto>> GetDatasetUsersAsync(string datasetId);
    Task<bool> ShareDatasetAsync(ShareDatasetRequest request, string sharedByUserId);

    /// <summary>Additively grants a single table to a user. Creates the share (restricted to that table)
    /// if the user has no access yet; never reduces a user who already has full dataset access.</summary>
    Task<bool> GrantTableAccessAsync(GrantTableShareRequest request, string sharedByUserId);
    Task<bool> UpdateDatasetUserTypeAsync(string datasetId, string userId, DatasetUserType userType);
    Task<bool> RemoveDatasetUserAsync(string datasetId, string userId);
    Task<List<Dataset>> GetSharedDatasetsAsync(string userId, string companyId);
    Task<bool> HasDatasetAccessAsync(string datasetId, string userId, DatasetUserType? requiredType = null);
}

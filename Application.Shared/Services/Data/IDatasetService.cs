using Application.Shared.Models;
using Application.Shared.Models.Data;

namespace Application.Shared.Services.Data;


public interface IDatasetService
{
    Task<Dataset?> GetDatasetAsync(string id, string userId);
    Task<List<Dataset>> GetDatasetsByCompanyAsync(string companyId, string userId);
    Task<List<Dataset>> GetDatasetsAsync(string userId);
    Task<Dataset?> CreateDatasetAsync(Dataset dataset, string userId);
    Task<Dataset?> UpdateDatasetAsync(string id, Dataset dataset, string userId);
    Task<bool> DeleteDatasetAsync(string id, string userId);

    // New methods for chat functionality
    Task<List<Dataset>> GetDatasetsByIdsAsync(List<string> datasetIds, string companyId, string userId);
    Task<List<Dataset>> SearchDatasetsAsync(string query, string companyId, string userId);
    
    // Table-level methods for chat functionality
    Task<List<TableSearchResult>> SearchTablesAsync(string query, string companyId, string userId);
    Task<TableReference?> GetTableWithDataAsync(string datasetId, string tableName, string companyId, string userId, int sampleRows = 10);
    Task<List<TableReference>> GetTablesByReferencesAsync(List<TableReference> tableReferences, string companyId, string userId, int sampleRows = 10);
}

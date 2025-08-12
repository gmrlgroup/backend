using Application.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Application.Shared.Models.Data;

namespace Application.Shared.Services.Data;

public interface IDuckdbService
{
    Task CreateDatabaseAsync(Dataset dataset);
    Task DeleteDatabaseAsync(Dataset dataset);
    Task UpdateDatabaseAsync(string datasetId, string updateQuery);
    Task<List<T>> ExecuteQueryAsync<T>(string databasePath, string query, Func<IDataReader, T> mapFunction);
    Task<List<T>> ExecuteQueryAsync<T>(Dataset dataset, string query, Func<IDataReader, T> mapFunction);
    Task<string> ExecuteQueryAsync(Dataset dataset, string query);
    Task<List<Column>> GetTableColumnsAsync(string datasetId, string tableName);
    Task<IEnumerable<string>> GetTablesAsync(string datasetId);
    Task<Table> GetTableAsync(string datasetId, string tableName);
    Task<Table> CreateTableAsync(Table table);
    Task<bool> DeleteTableAsync(string datasetId, string tableName);
    Task<bool> ImportCsvDataAsync(string datasetId, string tableName, Stream csvStream);
    Task<bool> ImportCsvDataAsync(string companyId, string datasetId, string tableName, Stream csvStream, bool createDataset = false, bool createTable = false);

    // New methods for data querying
    Task<TableDataResult> QueryTableDataAsync(TableDataQuery query);
    Task<int> GetTableRowCountAsync(string datasetId, string tableName, List<FilterCondition>? filters = null);
}

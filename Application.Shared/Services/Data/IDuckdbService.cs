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

    // Ad-hoc SQL workbench. Reads open a read-only DuckDB handle; writes (allowWrite) open a
    // read-write handle. SQL errors are returned via SqlQueryResult.Error, never thrown.
    Task<SqlQueryResult> ExecuteSqlAsync(string datasetId, string sql, bool allowWrite, int maxRows, System.Threading.CancellationToken ct = default);

    // Write-back: materialize a SELECT query as a new table or view in the dataset.
    Task<SqlQueryResult> CreateObjectFromQueryAsync(string datasetId, string objectName, string sql, bool asView, System.Threading.CancellationToken ct = default);

    // Stage a file into a temporary table and validate it against the target table's schema
    // (type-cast checks, missing/extra columns, preview rows) without committing anything.
    // Errors are returned via ImportValidationResult.Error, never thrown.
    Task<ImportValidationResult> ValidateImportAsync(string datasetId, string tableName, Stream fileStream, ImportFileFormat format, System.Threading.CancellationToken ct = default);

    // Like ValidateImportAsync but validates against a caller-supplied schema (the columns being defined
    // in the import wizard) rather than an existing table — true pre-commit validation for new tables.
    Task<ImportValidationResult> ValidateImportAgainstSchemaAsync(string datasetId, List<Column> targetColumns, Stream fileStream, ImportFileFormat format, System.Threading.CancellationToken ct = default);

    // Stage a file (no target table) and return the columns DuckDB infers plus a preview — lets the
    // wizard build a schema editor for formats the browser can't parse (JSON/Parquet/Excel).
    Task<FilePeekResult> PeekFileAsync(string datasetId, Stream fileStream, ImportFileFormat format, System.Threading.CancellationToken ct = default);

    // Stage a file and commit it into the target table with the chosen mode
    // (append / replace / upsert on keyColumns). Optionally skips rows that fail TRY_CAST.
    // Errors are returned via ImportResult.Error, never thrown.
    Task<ImportResult> ImportFileAsync(string datasetId, string tableName, Stream fileStream, ImportFileFormat format, ImportMode mode, List<string> keyColumns, bool skipInvalidRows, bool createIfMissing = false, System.Threading.CancellationToken ct = default);
}

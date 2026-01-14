using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace Application.Shared.Services
{
    public class DataWarehouseService
    {
        private readonly DataWarehouseDbContext _context;

        public DataWarehouseService(DataWarehouseDbContext context)
        {
            _context = context;
        }

        public async Task<DataTableResult> GetTableDataAsync(string tableName, int page = 1, int pageSize = 100)
        {
            // Validate table name to prevent SQL injection
            if (string.IsNullOrWhiteSpace(tableName) || !IsValidTableName(tableName))
            {
                throw new ArgumentException("Invalid table name", nameof(tableName));
            }

            var result = new DataTableResult();
            var connection = _context.Database.GetDbConnection();

            try
            {
                await connection.OpenAsync();

                // Get total count
                var countQuery = $"SELECT COUNT(*) FROM {tableName}";
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countQuery;
                    var countResult = await countCommand.ExecuteScalarAsync();
                    result.TotalRows = Convert.ToInt32(countResult);
                }

                // Get paginated data
                var offset = (page - 1) * pageSize;
                var query = $"SELECT * FROM {tableName} ORDER BY (SELECT NULL) OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Get column information
                        var schemaTable = reader.GetSchemaTable();
                        if (schemaTable != null)
                        {
                            foreach (DataRow row in schemaTable.Rows)
                            {
                                var columnName = row["ColumnName"].ToString();
                                var dataType = ((Type)row["DataType"]).Name;

                                result.Columns.Add(new ColumnInfo
                                {
                                    Name = columnName,
                                    DataType = dataType
                                });
                            }
                        }

                        // Read data
                        while (await reader.ReadAsync())
                        {
                            var rowData = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                rowData[reader.GetName(i)] = value;
                            }
                            result.Rows.Add(rowData);
                        }
                    }
                }

                result.CurrentPage = page;
                result.PageSize = pageSize;
                result.TotalPages = (int)Math.Ceiling((double)result.TotalRows / pageSize);
            }
            finally
            {
                await connection.CloseAsync();
            }

            return result;
        }

        public async Task<List<TableInfo>> GetTablesAsync()
        {
            var tables = new List<TableInfo>();
            var connection = _context.Database.GetDbConnection();

            try
            {
                await connection.OpenAsync();

                var query = @"
                    SELECT 
                        TABLE_SCHEMA,
                        TABLE_NAME,
                        TABLE_TYPE
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    ORDER BY TABLE_SCHEMA, TABLE_NAME";

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tables.Add(new TableInfo
                            {
                                Schema = reader.GetString(0),
                                Name = reader.GetString(1),
                                FullName = $"{reader.GetString(0)}.{reader.GetString(1)}"
                            });
                        }
                    }
                }
            }
            finally
            {
                await connection.CloseAsync();
            }

            return tables;
        }

        private bool IsValidTableName(string tableName)
        {
            // Basic validation: Check if table name matches pattern [schema].[table] or [table]
            // and doesn't contain suspicious characters
            if (string.IsNullOrWhiteSpace(tableName))
                return false;

            // Allow alphanumeric, underscore, dot, and square brackets
            var pattern = @"^(\[?[a-zA-Z_][a-zA-Z0-9_]*\]?\.)?(\[?[a-zA-Z_][a-zA-Z0-9_]*\]?)$";
            return System.Text.RegularExpressions.Regex.IsMatch(tableName, pattern);
        }
    }

    public class DataTableResult
    {
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
        public List<Dictionary<string, object>> Rows { get; set; } = new List<Dictionary<string, object>>();
        public int TotalRows { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }

    public class TableInfo
    {
        public string Schema { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }
}

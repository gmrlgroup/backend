using Application.Shared.Models;
using Application.Shared.Models.Data;
using Azure.Core;
using DuckDB.NET.Data;
//using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Shared.Services.Data;

public class DuckdbService : IDuckdbService
{
    private readonly DuckdbOption _option;
    //private readonly IDatasetService _datasetService;

    public DuckdbService(DuckdbOption option)
    {
        _option = option;
        //_datasetService = datasetService;
    }

    public DatasetService? DatasetService { get; set; }

    public async Task CreateDatabaseAsync(Dataset dataset)
    {

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{dataset.Id}.duckdb";

        if (File.Exists(duckdbFilePath))
            throw new InvalidOperationException("Database already exists.");

        // Create a new DuckDB database file
        // openning a connection will create the file
        using (var duckDBConnection = new DuckDBConnection($"Data Source={duckdbFilePath}"))
        {
            await duckDBConnection.OpenAsync();

            // close the connection
            await duckDBConnection.CloseAsync();
        }



    }

    public async Task DeleteDatabaseAsync(Dataset dataset)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{dataset.Id}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        await Task.Run(() => File.Delete(duckdbFilePath));
    }

    public async Task UpdateDatabaseAsync(string datasetId, string updateQuery)
    {

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = updateQuery;

        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
    }    
    
    public async Task<List<T>> ExecuteQueryAsync<T>(string databasePath, string query, Func<IDataReader, T> mapFunction)
    {
        if (!File.Exists(databasePath))
            throw new FileNotFoundException("Database not found.");

        var results = new List<T>();
        
        using var connection = new DuckDBConnection($"DataSource={databasePath}");
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = query;
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(mapFunction(reader));
        }
        
        await connection.CloseAsync();
        return results;
    }

    public async Task<List<T>> ExecuteQueryAsync<T>(Dataset dataset, string query, Func<IDataReader, T> mapFunction)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{dataset.Id}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        var results = new List<T>();
        
        using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = query;
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(mapFunction(reader));
        }
        
        await connection.CloseAsync();
        return results;
    }

    public async Task<string> ExecuteQueryAsync(Dataset dataset, string query)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{dataset.Id}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        // var results = new List<T>();

        using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = query;

        using var reader = await command.ExecuteReaderAsync();

        var results = new List<object>();
        while (await reader.ReadAsync())
        {
            // Assuming you want to return the first column of each row as a string
            if (reader.FieldCount > 0)
            {
                var value = reader.IsDBNull(0) ? "NULL" : reader.GetValue(0).ToString();
                results.Add(value);
            }
        }
        
        await connection.CloseAsync();

        // convert result to string
        return string.Join(", ", results.Select(r => r.ToString()));
    }

    public async Task<IEnumerable<string>> GetTablesAsync(string datasetId)
    {

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        try
        {
            // Query to get all tables from the database
            var tablesQuery = @"
                SELECT table_name 
                FROM information_schema.tables 
                WHERE table_schema NOT IN ('information_schema', 'pg_catalog')
                ORDER BY table_name;";

            var tables = await ExecuteQueryAsync(duckdbFilePath, tablesQuery, reader => reader.GetString(0));

            return tables;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve tables for dataset '{datasetId}': {ex.Message}", ex);
        }
    }

    public async Task<Table> GetTableAsync(string datasetId, string tableName)
    {

        try
        {
            var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

            // Query to get all tables from the database
            var tablesQuery = @$"SELECT table_name, table_schema,
                                FROM information_schema.tables 
                                WHERE table_schema NOT IN ('information_schema')
                                  AND table_catalog = '{datasetId}' -- name of the database (datasetId) 
                                  --AND table_name = {tableName}
                                ORDER BY table_name;";

            var tables = await ExecuteQueryAsync(duckdbFilePath, tablesQuery, reader => new
            {
                TableName = reader.GetString(0),
                SchemaName = reader.GetString(1) // Assuming you want to include schema name as well
            });            var table = tables.FirstOrDefault();
            
            if (table == null)
                throw new InvalidOperationException($"Table '{tableName}' not found in dataset '{datasetId}'.");

            Table t = new Table
            {
                TableName = table.TableName,
                SchemaName = table.SchemaName, // Assuming you want to include schema name as well
                DatasetId = datasetId,
                //CompanyId = dataset.CompanyId, // TODO: get the companyId from the header or from the dataset
                Columns = await GetTableColumnsAsync(datasetId, tableName)
            };

            return t;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to retrieve table '{tableName}': {ex.Message}", ex);
        }
    }


    public async Task<Table> CreateTableAsync(Table table)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{table.DatasetId}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        try
        {
            // Generate DuckDB CREATE TABLE query
            var createTableQuery = GenerateCreateTableQuery(table);

            // Execute the query
            await UpdateDatabaseAsync(table.DatasetId, createTableQuery);

            return table;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create table '{table.TableName}': {ex.Message}", ex);
        }
    }



    public async Task<bool> DeleteTableAsync(string datasetId, string tableName)
    {
        
        try
        {


            // Generate DROP TABLE query
            var dropTableQuery = $"DROP TABLE IF EXISTS {tableName};";



            // Execute the query using the correct method signature
            await UpdateDatabaseAsync(datasetId, dropTableQuery);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete table '{tableName}': {ex.Message}", ex);
        }
    }



    // create a function that returns the list of the columns from a table in the database
    public async Task<List<Column>> GetTableColumnsAsync(string datasetId, string tableName)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        List<Column> columns = new List<Column>();

        using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");

        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        command.CommandText = $"PRAGMA table_info('{tableName}')";

        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // col 5 is nullable and the values are false or true , convert it to bool
            Column col = new Column()
            {
                Name = reader.GetString(1),
                DataType = reader.GetString(2),
                //IsNullable = reader.GetString(3).ToLower() == "true" ? true : false,
                IsNullable = reader.GetBoolean(3),
                DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(5),
                //IsPrimaryKey = reader.GetString(5).ToLower() == "true" ? true : false,
                IsPrimaryKey = reader.GetBoolean(5)
            };
            columns.Add(col);

        }

        await connection.CloseAsync();
        
        return columns;
    }



    private string GenerateCreateTableQuery(Table table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {table.SchemaName}.{table.TableName} ("
        );

        var columnDefinitions = table.Columns.Select(col =>
        {
            var definition = $"    {col.Name} {col.DataType}";

            if (!col.IsNullable)
                definition += " NOT NULL";

            if (!string.IsNullOrWhiteSpace(col.DefaultValue))
                definition += $" DEFAULT {col.DefaultValue}";

            return definition;
        });

        sb.AppendLine(string.Join(",\n", columnDefinitions));
        sb.Append(");");

        return sb.ToString();
    }

    public async Task<bool> ImportCsvDataAsync(string datasetId, string tableName, Stream csvStream)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        try
        {
            // Save the CSV stream to a temporary file
            var tempCsvPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
            //string tempCsvPath = await CleanCsvStreamToTempFileAsync(csvStream, Encoding.GetEncoding("windows-1252"));


            //using (var fileStream = new FileStream(tempCsvPath, FileMode.Create, FileAccess.Write))
            //{
            //    await csvStream.CopyToAsync(fileStream);
            //}

            using (var reader = new StreamReader(csvStream, Encoding.GetEncoding("windows-1252")))
            using (var writer = new StreamWriter(tempCsvPath, false, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        // Replace problematic characters here
                        //line = line.Replace("\"", "_");   // This is causing error
                        line = line.Replace("&", "AND");  

                        await writer.WriteLineAsync(line);
                    }
                }
            }



            using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            // Use DuckDB's COPY command to import CSV data efficiently
            // This handles large files better than INSERT statements
            command.CommandText = $"COPY {tableName} FROM '{tempCsvPath.Replace("\\", "\\\\")}' (HEADER, DELIMITER ',', QUOTE '\"', ESCAPE '\"', SAMPLE_SIZE -1)"; //, IGNORE_ERRORS true

            //command.CommandText = $"""
            //        CREATE TABLE {tableName} AS 
            //        SELECT * 
            //        FROM read_csv_auto('{tempCsvPath.Replace("\\", "\\\\")}', 
            //                           HEADER=true, 
            //                           SAMPLE_SIZE=-1); -- Reads entire file for best type inference
            //        """;

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            // Clean up temporary file
            if (File.Exists(tempCsvPath))
            {
                File.Delete(tempCsvPath);
            }

            return true;
        }        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import CSV data into table '{tableName}': {ex.Message}", ex);
        }
    }

    public async Task<bool> ImportCsvDataAsync(string companyId, string datasetId, string tableName, Stream csvStream, bool createDataset = false, bool createTable = false)
    {
        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";

        if (createDataset)
        {
            // If the dataset does not exist, create it
            var dataset = new Dataset
            {
                Id = datasetId,
                Name = tableName, // You can set a more meaningful name
                CompanyId = companyId, // Set a default company ID or pass it as a parameter
                Description = "Imported dataset from CSV",
            };
            await CreateDatabaseAsync(dataset);
        }
        else
        {
            if (!File.Exists(duckdbFilePath))
                throw new FileNotFoundException("Database not found.");
        }

        

        

        

        try
        {
            // Save the CSV stream to a temporary file
            var tempCsvPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

            using (var fileStream = new FileStream(tempCsvPath, FileMode.Create, FileAccess.Write))
            {
                await csvStream.CopyToAsync(fileStream);
            }

            if (createTable)
            {
                // If the table does not exist, create it based on the CSV structure
                //using var reader = new StreamReader(csvStream);

                // read the file from tempCsvPath
                using var reader = new StreamReader(tempCsvPath);

                var headerLine = await reader.ReadLineAsync();

                if (headerLine == null)
                    throw new InvalidOperationException("CSV file is empty.");

                // remove columns with no name

                var columns = headerLine.Split(',').Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => new Column
                    {
                        Name = c.Trim(),
                        //DataType = "VARCHAR", // Default to TEXT, you can enhance this to infer types
                        IsNullable = true,
                        DefaultValue = null,
                        IsPrimaryKey = false
                    }).ToList();

                var table = new Table
                {
                    TableName = tableName,
                    SchemaName = "main", // Default schema, can be parameterized
                    DatasetId = datasetId,
                    Columns = columns
                };
                await CreateTableAsync(table);
            }

            using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();

            // Use DuckDB's COPY command to import CSV data efficiently
            // This handles large files better than INSERT statements
            command.CommandText = $"COPY {tableName} FROM '{tempCsvPath.Replace("\\", "\\\\")}' (HEADER, DELIMITER ',', ENCODING 'windows-1252')";

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            // Clean up temporary file
            if (File.Exists(tempCsvPath))
            {
                File.Delete(tempCsvPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import CSV data into table '{tableName}': {ex.Message}", ex);
        }
    }

    public async Task<TableDataResult> QueryTableDataAsync(TableDataQuery query)
    {

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{query.DatasetId}.duckdb";
        Console.WriteLine($"------- DuckDB file path: {duckdbFilePath}");
        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");


        // var dataset = DatasetService != null ? await DatasetService.GetDatasetAsync(query.DatasetId) : null;
        // if (dataset == null)
        //     throw new FileNotFoundException("Dataset not found.");

        

        try
        {
            using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
            await connection.OpenAsync();

            // Build the SQL query
            var sqlBuilder = new StringBuilder();
            
            // SELECT clause
            if (query.SelectedColumns?.Any() == true)
            {
                sqlBuilder.Append($"SELECT {string.Join(", ", query.SelectedColumns.Select(c => $"\"{c}\""))}");
            }
            else
            {
                sqlBuilder.Append("SELECT *");
            }
            
            sqlBuilder.Append($" FROM \"{query.TableName}\"");

            // WHERE clause for filters (reused for the COUNT below so the total matches the filtered view)
            var whereClause = BuildWhereClause(query.Filters);
            sqlBuilder.Append(whereClause);

            // ORDER BY clause
            if (query.SortColumns?.Any() == true)
            {
                var sortClauses = query.SortColumns.Select(s => $"\"{s.ColumnName}\" {(s.IsDescending ? "DESC" : "ASC")}");
                sqlBuilder.Append($" ORDER BY {string.Join(", ", sortClauses)}");
            }
            
            // LIMIT and OFFSET for pagination
            var offset = (query.Page - 1) * query.PageSize;
            sqlBuilder.Append($" LIMIT {query.PageSize} OFFSET {offset}");

            var result = new TableDataResult
            {
                Data = new List<Dictionary<string, object>>(),
                Page = query.Page,
                PageSize = query.PageSize
            };

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sqlBuilder.ToString();

                using var reader = await command.ExecuteReaderAsync();

                // Get column information
                var columns = new List<Column>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(new Column
                    {
                        Name = reader.GetName(i),
                        DataType = reader.GetFieldType(i).Name
                    });
                }
                result.Columns = columns;

                // Read data rows
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.IsDBNull(i) ? null : NormalizeValue(reader.GetValue(i));
                        row[reader.GetName(i)] = value ?? DBNull.Value;
                    }
                    result.Data.Add(row);
                }
            }

            // Total count for pagination — must reflect the full (filtered) result set, NOT just the
            // page returned, or the grid pager can't move past the first page. Run it on the SAME open
            // connection: opening a second connection to the same .duckdb file throws
            // "File is already open" (DuckDB allows one read-write handle per file per process).
            using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = $"SELECT COUNT(*) FROM \"{query.TableName}\"{whereClause}";
                var countScalar = await countCommand.ExecuteScalarAsync();
                result.TotalRows = Convert.ToInt32(countScalar);
            }

            await connection.CloseAsync();

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to query table data: {ex.Message}", ex);
        }
    }

    // ----- Ad-hoc SQL workbench --------------------------------------------------------------

    // Hard ceiling on rows the workbench will return, regardless of a requested MaxRows — keeps a
    // runaway "SELECT *" from materializing an entire table into memory.
    private const int MaxAdHocRows = 5000;
    private const int QueryTimeoutSeconds = 60;

    private enum SqlKind { Read, Write, Empty }

    public async Task<SqlQueryResult> ExecuteSqlAsync(string datasetId, string sql, bool allowWrite, int maxRows, CancellationToken ct = default)
    {
        var result = new SqlQueryResult();
        var stopwatch = Stopwatch.StartNew();

        var kind = ClassifyStatement(sql);
        if (kind == SqlKind.Empty)
        {
            result.Error = "Query is empty.";
            return result;
        }
        if (kind == SqlKind.Write && !allowWrite)
        {
            result.Error = "This query modifies data and requires edit permission.";
            return result;
        }

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";
        if (!File.Exists(duckdbFilePath))
        {
            result.Error = "Dataset database not found.";
            return result;
        }

        var cap = maxRows > 0 ? Math.Min(maxRows, MaxAdHocRows) : MaxAdHocRows;
        result.IsSelect = kind == SqlKind.Read;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(QueryTimeoutSeconds));

            // Reads use a read-only handle: multiple readers can coexist (no single-writer lock) and
            // a VIEW_DATA user physically cannot mutate, even via a crafted WITH. Writes need a
            // read-write handle (one writer per file, like imports).
            var connectionString = kind == SqlKind.Read
                ? $"DataSource={duckdbFilePath};ACCESS_MODE=READ_ONLY"
                : $"DataSource={duckdbFilePath}";

            using var connection = new DuckDBConnection(connectionString);
            await connection.OpenAsync(cts.Token);

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync(cts.Token);
            if (reader.FieldCount > 0)
                ReadResultSet(reader, result, cap, cts.Token);
            if (kind == SqlKind.Write)
                result.RowsAffected = reader.RecordsAffected;

            await connection.CloseAsync();
        }
        catch (OperationCanceledException)
        {
            result.Error = $"The query was cancelled or exceeded the {QueryTimeoutSeconds}s time limit.";
        }
        catch (Exception ex)
        {
            // Surface the DuckDB error message inline — the workbench shows it to the author.
            result.Error = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<SqlQueryResult> CreateObjectFromQueryAsync(string datasetId, string objectName, string sql, bool asView, CancellationToken ct = default)
    {
        var result = new SqlQueryResult();
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(objectName) || !Regex.IsMatch(objectName, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            result.Error = "Invalid name. Use letters, digits and underscores; it cannot start with a digit.";
            return result;
        }
        if (ClassifyStatement(sql) != SqlKind.Read)
        {
            result.Error = "Only a single SELECT query can be saved as a table or view.";
            return result;
        }

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";
        if (!File.Exists(duckdbFilePath))
        {
            result.Error = "Dataset database not found.";
            return result;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(QueryTimeoutSeconds));

            using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
            await connection.OpenAsync(cts.Token);

            using var command = connection.CreateCommand();
            var objectType = asView ? "VIEW" : "TABLE";
            var inner = sql.TrimEnd().TrimEnd(';'); // compose inside CREATE ... AS (...)
            command.CommandText = $"CREATE OR REPLACE {objectType} \"{objectName}\" AS {inner}";
            result.RowsAffected = await command.ExecuteNonQueryAsync(cts.Token);

            await connection.CloseAsync();
        }
        catch (OperationCanceledException)
        {
            result.Error = $"The operation exceeded the {QueryTimeoutSeconds}s time limit.";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    private static void ReadResultSet(DbDataReader reader, SqlQueryResult result, int cap, CancellationToken ct)
    {
        var columns = new List<Column>();
        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(new Column { Name = reader.GetName(i), DataType = reader.GetFieldType(i).Name });
        result.Columns = columns;

        int count = 0;
        while (reader.Read())
        {
            ct.ThrowIfCancellationRequested();
            if (count >= cap)
            {
                // There was at least one more row than the cap — flag it and stop.
                result.Truncated = true;
                break;
            }
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : NormalizeValue(reader.GetValue(i));
            result.Rows.Add(row);
            count++;
        }
        result.RowsReturned = result.Rows.Count;
    }

    // Some DuckDB types don't serialize cleanly to JSON as a plain value. The main offender is
    // HUGEINT (e.g. the result of SUM over an integer column), which DuckDB.NET surfaces as a
    // System.Numerics.BigInteger — System.Text.Json has no built-in support for it and would emit
    // {"isPowerOfTwo":...,"sign":...} instead of a number. Coerce it to a long (the common case),
    // falling back to its string form only when the value exceeds long's range.
    private static object? NormalizeValue(object? value)
    {
        if (value is null || value is DBNull) return null;

        if (value is System.Numerics.BigInteger big)
        {
            if (big >= long.MinValue && big <= long.MaxValue) return (long)big;
            return big.ToString();
        }

        return value;
    }

    // Routes a statement to the read or write path. Comment-stripping + first-keyword inspection is
    // enough to *route* (choose connection mode + required role); the read-only connection is the
    // hard guarantee that a misclassified write can't actually mutate.
    private static SqlKind ClassifyStatement(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return SqlKind.Empty;

        var cleaned = StripSqlComments(sql).Trim();
        if (cleaned.Length == 0) return SqlKind.Empty;

        // Keep the read path to a single statement; multi-statement input is treated as a write
        // (so it needs edit permission) and the read-only handle still blocks any mutation.
        var statements = cleaned.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (statements.Count == 0) return SqlKind.Empty;
        if (statements.Count > 1) return SqlKind.Write;

        var firstWord = new string(statements[0].TrimStart().TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        return firstWord is "SELECT" or "WITH" or "TABLE" or "FROM" or "VALUES" or "DESCRIBE" or "SHOW" or "PRAGMA" or "EXPLAIN" or "SUMMARIZE"
            ? SqlKind.Read
            : SqlKind.Write;
    }

    private static string StripSqlComments(string sql)
    {
        // Best-effort: drop /* block */ then -- line comments. Only used for routing, never executed.
        var noBlock = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(noBlock, @"--[^\n]*", " ");
    }

    // Builds the " WHERE ..." fragment (leading space included) from filters, or "" when there are none.
    private string BuildWhereClause(List<FilterCondition>? filters)
    {
        if (filters?.Any() != true)
            return string.Empty;

        var filterClauses = new List<string>();
        foreach (var filter in filters)
        {
            var filterClause = BuildFilterClause(filter);
            if (!string.IsNullOrEmpty(filterClause))
                filterClauses.Add(filterClause);
        }

        return filterClauses.Any() ? $" WHERE {string.Join(" AND ", filterClauses)}" : string.Empty;
    }

    public async Task<int> GetTableRowCountAsync(string datasetId, string tableName, List<FilterCondition>? filters = null)
    {
        // var dataset = DatasetService != null ? await DatasetService.GetDatasetAsync(datasetId) : null;
        // if (dataset == null)
        //     throw new FileNotFoundException("Dataset not found.");

        var duckdbFilePath = $"{_option.DuckdbFilePath}/{datasetId}.duckdb";
        if (!File.Exists(duckdbFilePath))
            throw new FileNotFoundException("Database not found.");

        try
        {
            using var connection = new DuckDBConnection($"DataSource={duckdbFilePath}");
            await connection.OpenAsync();

            var sqlBuilder = new StringBuilder($"SELECT COUNT(*) FROM \"{tableName}\"");
            
            // WHERE clause for filters
            if (filters?.Any() == true)
            {
                var filterClauses = new List<string>();
                foreach (var filter in filters)
                {
                    var filterClause = BuildFilterClause(filter);
                    if (!string.IsNullOrEmpty(filterClause))
                    {
                        filterClauses.Add(filterClause);
                    }
                }
                
                if (filterClauses.Any())
                {
                    sqlBuilder.Append($" WHERE {string.Join(" AND ", filterClauses)}");
                }
            }

            using var command = connection.CreateCommand();
            command.CommandText = sqlBuilder.ToString();
            
            var result = await command.ExecuteScalarAsync();
            await connection.CloseAsync();
            
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            
            throw new InvalidOperationException($"Failed to get table row count: {ex.Message}", ex);
        }
    }

    //private async Task<string> CleanCsvStreamToTempFileAsync(Stream csvStream, Encoding sourceEncoding)
    //{
    //    var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

    //    using (var reader = new StreamReader(csvStream, sourceEncoding))
    //    using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
    //    {
    //        while (!reader.EndOfStream)
    //        {
    //            var line = await reader.ReadLineAsync();
    //            if (line != null)
    //            {
    //                // Replace in the text
    //                line = line.Replace("&", "AND");
    //                await writer.WriteLineAsync(line);
    //            }
    //        }
    //    }

    //    return tempPath;
    //}


    private string BuildFilterClause(FilterCondition filter)
    {
        if (string.IsNullOrWhiteSpace(filter.ColumnName) || string.IsNullOrWhiteSpace(filter.Value))
            return string.Empty;

        var columnName = $"\"{filter.ColumnName}\"";
        var value = filter.Value.Replace("'", "''"); // Escape single quotes

        return filter.Operator.ToLower() switch
        {
            "equals" => $"{columnName} = '{value}'",
            "contains" => $"{columnName} LIKE '%{value}%'",
            "startswith" => $"{columnName} LIKE '{value}%'",
            "endswith" => $"{columnName} LIKE '%{value}'",
            "greaterthan" => $"{columnName} > '{value}'",
            "lessthan" => $"{columnName} < '{value}'",
            _ => $"{columnName} = '{value}'"
        };
    }
}

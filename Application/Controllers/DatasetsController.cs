using Application.Client.Pages.Data.DatasetPages;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;


namespace Application.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;
    private readonly IDuckdbService _duckdbService;

    public DatasetsController(IDatasetService datasetService, IDuckdbService duckdbService)
    {
        _datasetService = datasetService;
        _duckdbService = duckdbService;
    }

    // GET: api/Datasets/{companyId}
    [HttpGet()]
    public async Task<ActionResult<IEnumerable<Dataset>>> GetDatasets()
    {
        var companyId = Request.Headers["X-Company-ID"].FirstOrDefault();
        var userId = Request.Headers["UserId"].ToString();
        
        if (string.IsNullOrWhiteSpace(companyId))
            return BadRequest("Company ID is required");
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        var datasets = await _datasetService.GetDatasetsByCompanyAsync(companyId, userId);
        return Ok(datasets);
    }

    // GET: api/Datasets/item/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Dataset>> GetDataset(string id)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        var dataset = await _datasetService.GetDatasetAsync(id, userId);
        if (dataset == null)
            return NotFound();

        return Ok(dataset);
    }

    // POST: api/Datasets
    [HttpPost]
    public async Task<ActionResult<Dataset>> CreateDataset(Dataset dataset)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        try
        {
            var created = await _datasetService.CreateDatasetAsync(dataset, userId);
            if (created == null)
                return Conflict("Dataset already exists.");

            return CreatedAtAction(nameof(GetDataset), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error creating dataset: {ex.Message}");
        }
    }

    // PUT: api/Datasets/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<Dataset>> UpdateDataset(string id, Dataset dataset)
    {
        if (id != dataset.Id)
            return BadRequest("Mismatched dataset ID");

        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        try
        {
            var updated = await _datasetService.UpdateDatasetAsync(id, dataset, userId);
            if (updated == null)
                return NotFound();

            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error updating dataset: {ex.Message}");
        }
    }

    // DELETE: api/Datasets/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDataset(string id)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        try
        {
            var deleted = await _datasetService.DeleteDatasetAsync(id, userId);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest($"Error deleting dataset: {ex.Message}");
        }
    }

    // POST: api/Datasets/tables
    [HttpPost("tables")]
    public async Task<ActionResult> CreateTable(Table table)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(table.CompanyId))
            return BadRequest("Company ID is required");

        if (string.IsNullOrWhiteSpace(table.DatasetId))
            return BadRequest("Dataset ID is required");

        if (table.Columns == null || !table.Columns.Any())
            return BadRequest("At least one column is required");

        if (!await DatasetExists(table.DatasetId, userId))
            return NotFound($"Dataset with ID '{table.DatasetId}' not found.");


        await _duckdbService.CreateTableAsync(table);


        return CreatedAtAction(nameof(GetTable), new { datasetId = table.DatasetId, tableName = table.TableName }, table);
    }

    // GET: api/Datasets/{datasetId}/tables
    [HttpGet("{datasetId}/tables")]
    public async Task<ActionResult<IEnumerable<string>>> GetTables(string datasetId)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");        

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");

        var tables = await _duckdbService.GetTablesAsync(datasetId);

        if (tables == null || !tables.Any())
            return NotFound("No tables found in the dataset.");

        return Ok(tables);
    }


    // GET: api/Datasets/{datasetId}/tables
    [HttpGet("{datasetId}/tables/{tableName}")]
    public async Task<ActionResult<Table>> GetTable(string datasetId, string tableName)
    {
        var userId = Request.Headers["UserId"].ToString();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");


        return await _duckdbService.GetTableAsync(datasetId, tableName);

    }

    // get table columns
    // GET: api/Datasets/{datasetId}/tables/{tableName}/columns
    [HttpGet("{datasetId}/tables/{tableName}/columns")]
    public async Task<ActionResult<IEnumerable<Column>>> GetColumns(string datasetId, string tableName)
    {
        //var userId = Request.Headers["UserId"].ToString();
        //if (string.IsNullOrWhiteSpace(userId))
        //    return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required"); 
        
        try
        {
            return await _duckdbService.GetTableColumnsAsync(datasetId, tableName);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error retrieving tables: {ex.Message}");
        }
    }


    // DELETE: api/Datasets/{datasetId}/tables/{tableName}
    [HttpDelete("{datasetId}/tables/{tableName}")]
    public async Task<ActionResult<Table>> DeleteTable(string datasetId, string tableName)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (string.IsNullOrWhiteSpace(tableName))
            return BadRequest("Table name is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");


        var response = await _duckdbService.DeleteTableAsync(datasetId, tableName);

        if (!response)
            return NotFound($"Table '{tableName}' not found in dataset '{datasetId}'.");

        return NoContent();

    }

    // GET: api/Datasets/{datasetId}/tables/{tableName}/download
    [HttpGet("{datasetId}/tables/{tableName}/download")]
    public async Task<ActionResult> DownloadTable(string datasetId, string tableName)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (string.IsNullOrWhiteSpace(tableName))
            return BadRequest("Table name is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");

        try
        {
            // Create a query to get all table data (no pagination for download)
            var query = new TableDataQuery
            {
                DatasetId = datasetId,
                TableName = tableName,
                Page = 1,
                PageSize = int.MaxValue // Get all data for download
            };

            var tableDataResult = await _duckdbService.QueryTableDataAsync(query);
            
            if (tableDataResult?.Data == null || !tableDataResult.Data.Any())
                return NotFound($"No data found for table '{tableName}' in dataset '{datasetId}'.");

            // Convert data to CSV format
            var csvContent = new StringBuilder();
            
            // Add headers from columns if available, otherwise from first data row
            if (tableDataResult.Columns?.Any() == true)
            {
                var headers = string.Join(",", tableDataResult.Columns.Select(c => $"\"{c.Name}\""));
                csvContent.AppendLine(headers);
            }
            else if (tableDataResult.Data.Any())
            {
                var firstRow = tableDataResult.Data.First();
                var headers = string.Join(",", firstRow.Keys.Select(k => $"\"{k}\""));
                csvContent.AppendLine(headers);
            }
            
            // Add data rows
            foreach (var row in tableDataResult.Data)
            {
                var values = string.Join(",", row.Values.Select(v => $"\"{v?.ToString()?.Replace("\"", "\"\"") ?? ""}\""));
                csvContent.AppendLine(values);
            }

            var csvBytes = Encoding.UTF8.GetBytes(csvContent.ToString());
            var fileName = $"{tableName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error downloading table data: {ex.Message}");
        }
    }

    // POST: api/Datasets/{datasetId}/tables/{tableName}/download-filtered
    [HttpPost("{datasetId}/tables/{tableName}/download-filtered")]
    public async Task<ActionResult> DownloadFilteredTable(string datasetId, string tableName, [FromBody] TableDataQuery query)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (string.IsNullOrWhiteSpace(tableName))
            return BadRequest("Table name is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");

        try
        {
            // Ensure the query has the correct dataset and table information
            query.DatasetId = datasetId;
            query.TableName = tableName;
            
            // Set page size to max to get all filtered data
            query.PageSize = int.MaxValue;
            query.Page = 1;

            var tableDataResult = await _duckdbService.QueryTableDataAsync(query);
            
            if (tableDataResult?.Data == null || !tableDataResult.Data.Any())
                return NotFound($"No data found for table '{tableName}' with the applied filters.");

            // Convert data to CSV format
            var csvContent = new StringBuilder();
            
            // Add headers - use selected columns if specified, otherwise use all columns from result
            List<string> columnHeaders;
            if (query.SelectedColumns?.Any() == true)
            {
                columnHeaders = query.SelectedColumns;
            }
            else if (tableDataResult.Columns?.Any() == true)
            {
                columnHeaders = tableDataResult.Columns.Select(c => c.Name).ToList();
            }
            else if (tableDataResult.Data.Any())
            {
                columnHeaders = tableDataResult.Data.First().Keys.ToList();
            }
            else
            {
                return BadRequest("No columns available for export.");
            }

            // Add CSV headers
            var headers = string.Join(",", columnHeaders.Select(h => $"\"{h}\""));
            csvContent.AppendLine(headers);
            
            // Add data rows - only include selected columns
            foreach (var row in tableDataResult.Data)
            {
                var values = columnHeaders.Select(col => 
                {
                    var value = row.ContainsKey(col) ? row[col] : "";
                    return $"\"{value?.ToString()?.Replace("\"", "\"\"") ?? ""}\"";
                });
                csvContent.AppendLine(string.Join(",", values));
            }

            var csvBytes = Encoding.UTF8.GetBytes(csvContent.ToString());
            
            // Create descriptive filename
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var hasFilters = query.Filters?.Any() == true;
            var hasSelectedColumns = query.SelectedColumns?.Any() == true;
            
            string fileName;
            if (hasFilters && hasSelectedColumns)
            {
                fileName = $"{tableName}_filtered_custom_columns_{timestamp}.csv";
            }
            else if (hasFilters)
            {
                fileName = $"{tableName}_filtered_{timestamp}.csv";
            }
            else if (hasSelectedColumns)
            {
                fileName = $"{tableName}_custom_columns_{timestamp}.csv";
            }
            else
            {
                fileName = $"{tableName}_{timestamp}.csv";
            }

            return File(csvBytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error downloading filtered table data: {ex.Message}");
        }
    }

    // POST: api/Datasets/import-csv
    [HttpPost("import-csv")]
    [RequestSizeLimit(300_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 300_000_000)]
    public async Task<ActionResult> ImportCsvData()
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        try
        {
            // Check if the request contains multipart/form-data
            if (!Request.HasFormContentType)
                return BadRequest("Request must be multipart/form-data");

            var form = await Request.ReadFormAsync();
            
            // Get the required form data
            if (!form.TryGetValue("datasetId", out var datasetIdValues) || 
                string.IsNullOrWhiteSpace(datasetIdValues.FirstOrDefault()))
                return BadRequest("Dataset ID is required");

            if (!form.TryGetValue("tableName", out var tableNameValues) || 
                string.IsNullOrWhiteSpace(tableNameValues.FirstOrDefault()))
                return BadRequest("Table name is required");

            var datasetId = datasetIdValues.First()!;
            var tableName = tableNameValues.First()!;

            // Get the CSV file
            if (form.Files.Count == 0)
                return BadRequest("CSV file is required");

            var csvFile = form.Files[0];
            if (csvFile.Length == 0)
                return BadRequest("CSV file cannot be empty");

            // Validate file type
            if (!csvFile.ContentType.StartsWith("text/csv") && 
                !Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be a CSV file");

            // Check if dataset exists
            if (!await DatasetExists(datasetId, userId))
                return NotFound($"Dataset with ID '{datasetId}' not found.");

            // Import the CSV data
            using var csvStream = csvFile.OpenReadStream();
            var success = await _duckdbService.ImportCsvDataAsync(datasetId, tableName, csvStream);

            if (!success)
                return StatusCode(500, "Failed to import CSV data");

            return Ok(new { message = "CSV data imported successfully", datasetId, tableName });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error importing CSV data: {ex.Message}");
        }
    }

    // POST: api/Datasets/import-csv
    [HttpPost("external/import-csv")]
    public async Task<ActionResult> ImportExternalCsvData()
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        try
        {
            // Check if the request contains multipart/form-data
            if (!Request.HasFormContentType)
                return BadRequest("Request must be multipart/form-data");

            var form = await Request.ReadFormAsync();

            // Get the required form data
            if (!form.TryGetValue("datasetId", out var datasetIdValues) ||
                string.IsNullOrWhiteSpace(datasetIdValues.FirstOrDefault()))
                return BadRequest("Dataset ID is required");

            if (!form.TryGetValue("tableName", out var tableNameValues) ||
                string.IsNullOrWhiteSpace(tableNameValues.FirstOrDefault()))
                return BadRequest("Table name is required");

            if (!form.TryGetValue("companyId", out var companyIdValues) ||
                string.IsNullOrWhiteSpace(tableNameValues.FirstOrDefault()))
                return BadRequest("Company Id is required");

            var datasetId = datasetIdValues.First()!;
            var tableName = tableNameValues.First()!;
            var companyId = companyIdValues.First()!;


            // Get the CSV file
            if (form.Files.Count == 0)
                return BadRequest("CSV file is required");

            var csvFile = form.Files[0];
            if (csvFile.Length == 0)
                return BadRequest("CSV file cannot be empty");

            // Validate file type
            if (!csvFile.ContentType.StartsWith("text/csv") &&
                !Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be a CSV file");

            
            // If the dataset does not exist, create it
            //var dataset = new Dataset
            //{
            //    Id = datasetId,
            //    Name = tableName, // You can set a more meaningful name
            //    CompanyId = companyId, // Set a default company ID or pass it as a parameter
            //    Description = "Imported dataset from CSV",
            //};
            //await _duckdbService.CreateDatabaseAsync(dataset);
            

            // Check if dataset exists
            //if (!await DatasetExists(datasetId))
            //    return NotFound($"Dataset with ID '{datasetId}' not found.");

            // Import the CSV data
            using var csvStream = csvFile.OpenReadStream();
            var success = await _duckdbService.ImportCsvDataAsync(companyId, datasetId, tableName, csvStream, createDataset: true, createTable: true);

            if (!success)
                return StatusCode(500, "Failed to import CSV data");

            return Ok(new { message = "CSV data imported successfully", datasetId, tableName });
        }
        catch (Exception ex)
        {
            return BadRequest($"Error importing CSV data: {ex.Message}");
        }
    }

    // GET: api/Datasets/{datasetId}/tables/{tableName}/data
    [HttpPost("{datasetId}/tables/{tableName}/data")]
    public async Task<ActionResult<TableDataResult>> GetTableData(string datasetId, string tableName, [FromBody] TableDataQuery query)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (string.IsNullOrWhiteSpace(tableName))
            return BadRequest("Table name is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");

        try
        {
            query.DatasetId = datasetId;
            query.TableName = tableName;
            
            var result = await _duckdbService.QueryTableDataAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error querying table data: {ex.Message}");
        }
    }

    // GET: api/Datasets/{datasetId}/tables/{tableName}/count
    [HttpPost("{datasetId}/tables/{tableName}/count")]
    public async Task<ActionResult<int>> GetTableRowCount(string datasetId, string tableName, [FromBody] List<FilterCondition>? filters = null)
    {
        var userId = Request.Headers["UserId"].ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required in headers");

        if (string.IsNullOrWhiteSpace(datasetId))
            return BadRequest("Dataset ID is required");

        if (string.IsNullOrWhiteSpace(tableName))
            return BadRequest("Table name is required");

        if (!await DatasetExists(datasetId, userId))
            return NotFound($"Dataset with ID '{datasetId}' not found.");

        try
        {
            var count = await _duckdbService.GetTableRowCountAsync(datasetId, tableName, filters);
            return Ok(count);
        }
        catch (Exception ex)
        {
            return BadRequest($"Error getting table row count: {ex.Message}");
        }
    }

    private async Task<bool> DatasetExists(string id, string userId)
    {
        return await _datasetService.GetDatasetAsync(id, userId) != null;
    }

}

using Application.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataWarehouseController : ControllerBase
    {
        private readonly DataWarehouseService _dataWarehouseService;
        private readonly ILogger<DataWarehouseController> _logger;

        public DataWarehouseController(
            DataWarehouseService dataWarehouseService,
            ILogger<DataWarehouseController> logger)
        {
            _dataWarehouseService = dataWarehouseService;
            _logger = logger;
        }

        /// <summary>
        /// Get list of all tables in the data warehouse
        /// </summary>
        [HttpGet("tables")]
        public async Task<ActionResult<List<TableInfo>>> GetTables()
        {
            try
            {
                var tables = await _dataWarehouseService.GetTablesAsync();
                return Ok(tables);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tables from data warehouse");
                return StatusCode(500, new { message = "Error retrieving tables", error = ex.Message });
            }
        }

        /// <summary>
        /// Get data from a specific table
        /// </summary>
        /// <param name="tableName">The full table name (e.g., schema.tablename)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Number of rows per page (default: 100, max: 1000)</param>
        [HttpGet("data")]
        public async Task<ActionResult<DataTableResult>> GetTableData(
            [FromQuery] string tableName,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return BadRequest(new { message = "Table name is required" });
                }

                if (page < 1)
                {
                    return BadRequest(new { message = "Page must be greater than 0" });
                }

                if (pageSize < 1 || pageSize > 1000)
                {
                    return BadRequest(new { message = "Page size must be between 1 and 1000" });
                }

                var result = await _dataWarehouseService.GetTableDataAsync(tableName, page, pageSize);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid table name: {TableName}", tableName);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data from table: {TableName}", tableName);
                return StatusCode(500, new { message = "Error retrieving table data", error = ex.Message });
            }
        }
    }
}

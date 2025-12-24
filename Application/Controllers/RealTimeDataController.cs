using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Application.Services.Data;
using Application.Attributes;
using System.Security.Claims;

namespace Application.Controllers;


[ApiController]
[Route("api/[controller]")]
public class RealTimeDataController : ControllerBase
{
    private readonly ILogger<RealTimeDataController> _logger;
    private readonly IRealTimeDataService _realTimeDataService;
    private readonly ISalesDataSignalRService _salesDataSignalRService;

    public RealTimeDataController(
        ILogger<RealTimeDataController> logger,
        IRealTimeDataService realTimeDataService,
        ISalesDataSignalRService salesDataSignalRService)
    {
        _logger = logger;
        _realTimeDataService = realTimeDataService;
        _salesDataSignalRService = salesDataSignalRService;
    }

    [HttpPost("sales")]
    [RequireCompanyHeader]
    public async Task<ActionResult<SalesData>> CreateSalesData([FromBody] List<SalesData> salesData)
    {
        try
        {
            // var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            foreach(var rec in salesData)
            {
                rec.CompanyId = companyId;
            }
            

            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(salesData));

            var result = await _realTimeDataService.CreateSalesDataListAsync(salesData);
            if (result == null)
            {
                return BadRequest("Failed to create sales data");
            }

            // Broadcast the new sales data via SignalR
            await _salesDataSignalRService.BroadcastSalesDataListAsync(salesData, companyId);

            return CreatedAtAction(nameof(GetSalesDataById), new { id = result.FirstOrDefault()?.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating sales data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sales")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesData>>> GetSalesData(
        [FromQuery] int? fromHour = null,
        [FromQuery] int? toHour = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var salesData = await _realTimeDataService.GetSalesDataAsync(companyId, userId, fromHour, toHour);
            return Ok(salesData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sales/{id}")]
    [RequireCompanyHeader]
    public async Task<ActionResult<SalesData>> GetSalesDataById(string id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var salesData = await _realTimeDataService.GetSalesDataByIdAsync(id, companyId, userId);
            if (salesData == null)
            {
                return NotFound();
            }

            return Ok(salesData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales data {SalesDataId} for user {UserId}", 
                id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sales/store/{storeCode}")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesData>>> GetSalesDataByStore(
        string storeCode,
        [FromQuery] int? fromHour = null,
        [FromQuery] int? toHour = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var salesData = await _realTimeDataService.GetSalesDataByStoreAsync(companyId, storeCode, userId, fromHour, toHour);
            return Ok(salesData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales data for store {StoreCode} and user {UserId}", 
                storeCode, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sales/scheme/{scheme}")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesData>>> GetSalesDataByScheme(
        string scheme,
        [FromQuery] int? fromHour = null,
        [FromQuery] int? toHour = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var salesData = await _realTimeDataService.GetSalesDataBySchemeAsync(companyId, scheme, userId, fromHour, toHour);
            return Ok(salesData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales data for scheme {Scheme} and user {UserId}", 
                scheme, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("sales/{id}/process")]
    [RequireCompanyHeader]
    public async Task<ActionResult> MarkSalesDataAsProcessed(string id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            var result = await _realTimeDataService.MarkSalesDataAsProcessedAsync(id, userId);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking sales data {SalesDataId} as processed for user {UserId}", 
                id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("sales/{id}")]
    [RequireCompanyHeader]
    public async Task<ActionResult> DeleteSalesData(string id)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";

            var result = await _realTimeDataService.DeleteSalesDataAsync(id, userId);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sales data {SalesDataId} for user {UserId}", 
                id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sales/unprocessed")]
    [RequireCompanyHeader]
    public async Task<ActionResult<List<SalesData>>> GetUnprocessedSalesData()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var salesData = await _realTimeDataService.GetUnprocessedSalesDataAsync(companyId, userId);
            return Ok(salesData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unprocessed sales data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("analytics/summary")]
    [RequireCompanyHeader]
    public async Task<ActionResult<object>> GetSalesAnalyticsSummary(
        [FromQuery] int? fromHour = null,
        [FromQuery] int? toHour = null)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var totalAmount = await _realTimeDataService.GetTotalNetAmountAsync(companyId, userId, fromHour, toHour);
            var totalTransactions = await _realTimeDataService.GetTotalTransactionsAsync(companyId, userId, fromHour, toHour);
            var salesCount = await _realTimeDataService.GetSalesDataCountAsync(companyId, userId, fromHour, toHour);
            var salesByStore = await _realTimeDataService.GetSalesByStoreAsync(companyId, userId, fromHour, toHour);
            var salesByScheme = await _realTimeDataService.GetSalesBySchemeAsync(companyId, userId, fromHour, toHour);

            var summary = new
            {
                TotalNetAmount = totalAmount,
                TotalTransactions = totalTransactions,
                SalesRecordsCount = salesCount,
                SalesByStore = salesByStore,
                SalesByScheme = salesByScheme,
                Period = new
                {
                    FromHour = fromHour,
                    ToHour = toHour
                }
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sales analytics summary for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }
}

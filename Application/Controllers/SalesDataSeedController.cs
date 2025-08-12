using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Application.Shared.Models.Data;
using Application.Shared.Services.Data;
using Application.Services.Data;
using Application.Attributes;
using System.Security.Claims;

namespace Application.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SalesDataSeedController : ControllerBase
{
    private readonly ILogger<SalesDataSeedController> _logger;
    private readonly IRealTimeDataService _realTimeDataService;
    private readonly ISalesDataSignalRService _salesDataSignalRService;

    public SalesDataSeedController(
        ILogger<SalesDataSeedController> logger,
        IRealTimeDataService realTimeDataService,
        ISalesDataSignalRService salesDataSignalRService)
    {
        _logger = logger;
        _realTimeDataService = realTimeDataService;
        _salesDataSignalRService = salesDataSignalRService;
    }

    [HttpPost("generate-sample-data")]
    [RequireCompanyHeader]
    public async Task<ActionResult> GenerateSampleData([FromQuery] int count = 10)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            var stores = new[] { "STORE001", "STORE002", "STORE003", "STORE004", "STORE005" };
            var schemes = new[] { "RETAIL", "WHOLESALE", "ONLINE", "FRANCHISE" };
            var random = new Random();

            var createdData = new List<SalesData>();

            for (int i = 0; i < count; i++)
            {
                var salesData = new SalesData
                {
                    CompanyId = companyId,
                    Scheme = schemes[random.Next(schemes.Length)],
                    StoreCode = stores[random.Next(stores.Length)],
                    Hour = random.Next(0, 24),
                    NetAmountAcy = (decimal)(random.NextDouble() * 5000 + 100), // $100 - $5100
                    TotalTransactions = random.Next(1, 25),
                    Source = "SAMPLE_DATA_GENERATOR"
                };

                var result = await _realTimeDataService.CreateSalesDataAsync(salesData);
                if (result != null)
                {
                    createdData.Add(result);
                    
                    // Broadcast the new data
                    await _salesDataSignalRService.BroadcastSalesDataAsync(result, companyId);
                    
                    // Add small delay to simulate real-time data flow
                    await Task.Delay(100);
                }
            }

            return Ok(new { 
                message = $"Generated {createdData.Count} sample sales records",
                data = createdData.Select(d => new { 
                    d.Id, 
                    d.Scheme, 
                    d.StoreCode, 
                    d.NetAmountAcy, 
                    d.TotalTransactions 
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sample data for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("generate-live-feed")]
    [RequireCompanyHeader]
    public async Task<ActionResult> StartLiveFeed([FromQuery] int intervalSeconds = 5, [FromQuery] int durationMinutes = 5)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
            var companyId = Request.Headers["X-Company-ID"].FirstOrDefault() ?? "unknown";

            // Start background task for live data generation
            _ = Task.Run(async () =>
            {
                var stores = new[] { "STORE001", "STORE002", "STORE003", "STORE004", "STORE005" };
                var schemes = new[] { "RETAIL", "WHOLESALE", "ONLINE", "FRANCHISE" };
                var random = new Random();
                var endTime = DateTime.UtcNow.AddMinutes(durationMinutes);

                while (DateTime.UtcNow < endTime)
                {
                    try
                    {
                        var salesData = new SalesData
                        {
                            CompanyId = companyId,
                            Scheme = schemes[random.Next(schemes.Length)],
                            StoreCode = stores[random.Next(stores.Length)],
                            Hour = DateTime.UtcNow.Hour,
                            NetAmountAcy = (decimal)(random.NextDouble() * 3000 + 50), // $50 - $3050
                            TotalTransactions = random.Next(1, 15),
                            Source = "LIVE_FEED_GENERATOR"
                        };

                        var result = await _realTimeDataService.CreateSalesDataAsync(salesData);
                        if (result != null)
                        {
                            await _salesDataSignalRService.BroadcastSalesDataAsync(result, companyId);
                        }

                        await Task.Delay(intervalSeconds * 1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in live feed generation");
                    }
                }
            });

            return await Task.FromResult(Ok(new { 
                message = $"Started live data feed for {durationMinutes} minutes with {intervalSeconds}s intervals",
                intervalSeconds,
                durationMinutes
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting live feed for user {UserId}", 
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");
            return StatusCode(500, "Internal server error");
        }
    }
}

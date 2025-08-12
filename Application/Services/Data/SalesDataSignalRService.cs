using Application.Hubs;
using Application.Shared.Models.Data;
using Application.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace Application.Services.Data;

public interface ISalesDataSignalRService
{
    Task BroadcastSalesDataAsync(SalesData salesData, string companyId);
}

public class SalesDataSignalRService : ISalesDataSignalRService
{
    private readonly IHubContext<SalesDataHub> _hubContext;
    private readonly ILogger<SalesDataSignalRService> _logger;

    public SalesDataSignalRService(
        IHubContext<SalesDataHub> hubContext,
        ILogger<SalesDataSignalRService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastSalesDataAsync(SalesData salesData, string companyId)
    {
        try
        {
            var notification = new Notification<SalesData>
            {
                Message = $"New sales data received from store {salesData.StoreCode}",
                Data = salesData
            };

            //await _hubContext.Clients.Group($"Company_{companyId}")
            //    .SendAsync("ReceiveSalesData", notification);

            await _hubContext.Clients.All.SendAsync("ReceiveSalesData", notification);

            _logger.LogInformation("Sales data broadcasted to company group: {CompanyId}", companyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting sales data for company: {CompanyId}", companyId);
        }
    }
}

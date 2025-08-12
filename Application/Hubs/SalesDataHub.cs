using Application.Shared.Models.Data;
using Application.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Application.Hubs;

[Authorize]
public class SalesDataHub : Hub
{
    public async Task JoinCompanyGroup(string companyId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Company_{companyId}");
    }

    public async Task LeaveCompanyGroup(string companyId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Company_{companyId}");
    }

    public async Task SendSalesDataUpdate(Notification<SalesData> notification, string companyId)
    {
        await Clients.Group($"Company_{companyId}")
            .SendAsync("ReceiveSalesData", notification);
    }

    public override async Task OnConnectedAsync()
    {
        // You can add logic here to automatically join groups based on user claims
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up logic if needed
        await base.OnDisconnectedAsync(exception);
    }
}

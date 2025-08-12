using Application.Shared.Models;
using Microsoft.AspNetCore.SignalR;


namespace Application.Hubs;

public class NotificationHub<T> : Hub
{
    public async Task SendMessage(Notification<T> notification)
    {

        await Clients.All.SendAsync("ReceiveMessage", notification);
        
    }
}

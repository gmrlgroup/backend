using Application.Hubs;
using Application.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {

        private readonly IHubContext<NotificationHub<DataJob>> _hubContext;

        public NotificationsController(IHubContext<NotificationHub<DataJob>> hubContext)
        {
            _hubContext = hubContext;
        }

        // POST: api/rt/sales
        [HttpPost("data/jobs/dagster")]
        public async Task<IActionResult> SendData(Notification<DataJob> notification)
        {


            // Push data to all connected clients
            //await _hubContext.Clients.All.SendAsync("ReceiveData", salesLinesGrouped.Sum(s => s.NetAmountAcy));
           
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", notification);

            // add sales lines to db
            //await _realTimeDataService.AddSalesLineRealTimeToDb(salesLines);



            return Ok(new { Message = "Data sent to clients successfully." });
        }



    }
}

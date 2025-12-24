using System.Collections.Generic;

namespace Application.Scheduler.Options;

public class SalesSnapshotEmailOptions
{
    public string? ApiBaseUri { get; set; }
    public string Endpoint { get; set; } = "/api/email/sales/snapshot";
    public string? From { get; set; }
    public List<string> Recipients { get; set; } = new();
    public string? RecipientName { get; set; }
    public string? CompanyId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Beirut";
    public string CurrencyCulture { get; set; } = "en-US";
}

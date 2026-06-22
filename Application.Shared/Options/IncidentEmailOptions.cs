namespace Application.Shared.Options;

/// <summary>
/// Configuration for incident notification emails. Bound from the
/// "IncidentNotificationEmail" appsettings section in both the web app and the scheduler.
/// </summary>
public class IncidentEmailOptions
{
    /// <summary>Base URI of the Next.js email service (same service as the sales snapshot email).</summary>
    public string? ApiBaseUri { get; set; }

    /// <summary>Route on the email service that renders/sends the incident email.</summary>
    public string Endpoint { get; set; } = "/api/email/incident";

    /// <summary>From address used for the email.</summary>
    public string? From { get; set; }

    /// <summary>Public base URL of the app, used to build a link to the incident status page.</summary>
    public string? AppBaseUri { get; set; }
}

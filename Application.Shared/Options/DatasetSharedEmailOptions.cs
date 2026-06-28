namespace Application.Shared.Options;

/// <summary>
/// Configuration for "dataset shared" notification emails. Bound from the "DatasetSharedEmail"
/// appsettings section. Sends through the same Next.js/Resend email service as the other emails.
/// </summary>
public class DatasetSharedEmailOptions
{
    /// <summary>Base URI of the Next.js email service (same service as incident/sales-snapshot emails).</summary>
    public string? ApiBaseUri { get; set; }

    /// <summary>Route on the email service that renders/sends the dataset-shared email.</summary>
    public string Endpoint { get; set; } = "/api/email/dataset-shared";

    /// <summary>From address used for the email.</summary>
    public string? From { get; set; }

    /// <summary>Public base URL of the app, used to build a link to the shared dataset.</summary>
    public string? AppBaseUri { get; set; }
}

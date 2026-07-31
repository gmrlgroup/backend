namespace Application.Shared.Models;

/// <summary>Wire shape for the per-company settings API (shared by the server controller and the client).</summary>
public class CompanySettingsDto
{
    public bool DebugLoggingEnabled { get; set; }

    /// <summary>
    /// Date pattern for CSV exports, one of <see cref="ExportDateFormats.Allowed"/>. On a PUT, null means
    /// "leave unchanged" so a caller that only means to toggle debug logging can't blank it.
    /// </summary>
    public string? ExportDateFormat { get; set; }
}

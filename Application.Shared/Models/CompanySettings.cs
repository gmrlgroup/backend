namespace Application.Shared.Models;

/// <summary>
/// Per-company application settings (one row per company). Holds the debug-logging toggle that a
/// <c>{companyId}_ADMIN</c> flips to start capturing debug entries into the data_app_log store, and the
/// date format used for CSV exports.
/// </summary>
public class CompanySettings : BaseModel
{
    public int Id { get; set; }

    /// <summary>When true, feature code emits debug log entries for this company (see IDebugLogService).</summary>
    public bool DebugLoggingEnabled { get; set; }

    /// <summary>
    /// Date pattern for CSV exports, one of <see cref="ExportDateFormats.Allowed"/>. Null when the company
    /// has never chosen one — read it through <see cref="ExportDateFormats.Resolve"/> to get the default.
    /// </summary>
    public string? ExportDateFormat { get; set; }
}

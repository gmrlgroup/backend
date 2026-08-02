using System;
using System.Collections.Generic;
using System.Linq;

namespace Application.Shared.Models;

/// <summary>
/// The date patterns a company may pick for CSV exports, shared by the settings UI (dropdown) and the API
/// (validation) so the two can't drift apart.
/// <para>
/// Deliberately a fixed allowlist rather than a free-text field: the stored value is handed straight to
/// <c>DateTime.ToString</c> on the export path, where an arbitrary pattern could throw mid-download or
/// silently emit nonsense, and a custom pattern can't be meaningfully validated.
/// </para>
/// </summary>
public static class ExportDateFormats
{
    /// <summary>
    /// Day-first. The app default: exports previously rendered whatever the server's culture produced
    /// (MM/dd/yyyy on a US-culture host), which reads as the wrong date for a day-first audience.
    /// </summary>
    public const string Default = "dd/MM/yyyy";

    /// <summary>The time part appended to values that carry one — see <see cref="ForTimestamp"/>.</summary>
    private const string TimeSuffix = " HH:mm:ss";

    /// <summary>Selectable patterns, in the order the settings dropdown lists them.</summary>
    public static readonly IReadOnlyList<string> Allowed = new[]
    {
        "dd/MM/yyyy",
        "MM/dd/yyyy",
        "yyyy-MM-dd",
        "dd-MM-yyyy",
        "dd.MM.yyyy",
    };

    public static bool IsAllowed(string? format)
        => format != null && Allowed.Contains(format, StringComparer.Ordinal);

    /// <summary>
    /// The pattern to actually format with: the stored value when it's one we recognise, otherwise the
    /// default. Keeps a stale or hand-edited database value from breaking a download.
    /// </summary>
    public static string Resolve(string? stored) => IsAllowed(stored) ? stored! : Default;

    /// <summary>
    /// The date pattern extended with a time part, for values that actually carry a time. Exporting a
    /// timestamp through a date-only pattern would silently drop the time, which is data loss in an export.
    /// </summary>
    public static string ForTimestamp(string dateFormat) => dateFormat + TimeSuffix;
}

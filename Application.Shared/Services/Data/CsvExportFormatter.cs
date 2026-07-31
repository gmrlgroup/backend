using System;
using System.Globalization;
using Application.Shared.Models;

namespace Application.Shared.Services.Data;

/// <summary>
/// Renders queried cell values into CSV fields for table downloads.
/// <para>
/// Exists because the download endpoints used to call a bare <c>value.ToString()</c>, which formats using
/// the <em>server's</em> current culture. That made exported dates come out as MM/dd/yyyy on a US-culture
/// host, and would corrupt the file outright on a comma-decimal host (a decimal rendered as "1,5" injects a
/// field separator). Everything here formats with <see cref="CultureInfo.InvariantCulture"/> and an explicit
/// pattern, so output depends only on the configured format — never on the host's regional settings.
/// </para>
/// </summary>
public static class CsvExportFormatter
{
    /// <summary>
    /// Fixed ISO 8601 date pattern for machine consumers (the API-key export). Deliberately not the
    /// company's display preference: an external contract shouldn't change shape because someone picked a
    /// different format in the settings UI.
    /// </summary>
    public const string IsoDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// One value as a quoted, escaped CSV field. <paramref name="dateFormat"/> should come from
    /// <see cref="ExportDateFormats"/> (use <see cref="ExportDateFormats.Resolve"/> on stored values).
    /// </summary>
    public static string Field(object? value, string dateFormat) => Quote(Render(value, dateFormat));

    /// <summary>Wraps a field in quotes and doubles any embedded quote, as the export endpoints always have.</summary>
    public static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string Render(object? value, string dateFormat) => value switch
    {
        null or DBNull => string.Empty,

        // A DATE column arrives as a DateTime at midnight, so "no time component" is the signal to use the
        // date-only pattern. Anything carrying a time keeps it — formatting a timestamp through a date-only
        // pattern would silently discard information the user asked to export.
        DateTime dt => dt.ToString(
            dt.TimeOfDay == TimeSpan.Zero ? dateFormat : ExportDateFormats.ForTimestamp(dateFormat),
            CultureInfo.InvariantCulture),

        // Same date/time rule; the offset itself is dropped because these downloads are read as local
        // wall-clock values, and no supported source type round-trips an offset into this path.
        DateTimeOffset dto => dto.ToString(
            dto.TimeOfDay == TimeSpan.Zero ? dateFormat : ExportDateFormats.ForTimestamp(dateFormat),
            CultureInfo.InvariantCulture),

        DateOnly d => d.ToString(dateFormat, CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH:mm:ss", CultureInfo.InvariantCulture),

        // Numerics and other formattables: invariant, so the decimal separator can never become a comma.
        // (bool isn't IFormattable, so it keeps its existing "True"/"False" rendering below.)
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),

        _ => value.ToString() ?? string.Empty
    };
}

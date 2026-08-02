using Application.Shared.Data;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Shared.Services;

public interface ICompanySettingsService
{
    /// <summary>The company's settings row, or a transient default (debug off, default export format) when none is saved yet.</summary>
    Task<CompanySettings> GetAsync(string companyId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the company's settings and refreshes the cached values. A null
    /// <see cref="CompanySettingsDto.ExportDateFormat"/> leaves the stored format alone, so a caller that
    /// only means to toggle debug logging can't blank it.
    /// </summary>
    Task SaveAsync(string companyId, CompanySettingsDto settings, string? userId, CancellationToken ct = default);

    /// <summary>Upserts the debug-logging toggle for a company and refreshes the cached value.</summary>
    Task SetDebugLoggingAsync(string companyId, bool enabled, string? userId, CancellationToken ct = default);

    /// <summary>
    /// Cheap, cached read for the log write-path: true when this company has debug logging enabled.
    /// Cached for a short window so per-request logging doesn't hit the database on every entry.
    /// </summary>
    Task<bool> IsDebugLoggingEnabledAsync(string companyId, CancellationToken ct = default);

    /// <summary>
    /// Cached read for the CSV export path: the date pattern to write dates with, already resolved to a
    /// supported value (falls back to <see cref="ExportDateFormats.Default"/>).
    /// </summary>
    Task<string> GetExportDateFormatAsync(string companyId, CancellationToken ct = default);
}

public class CompanySettingsService : ICompanySettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public CompanySettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private static string DebugCacheKey(string companyId) => $"company-settings:debug:{companyId}";
    private static string ExportFormatCacheKey(string companyId) => $"company-settings:export-date-format:{companyId}";

    public async Task<CompanySettings> GetAsync(string companyId, CancellationToken ct = default)
    {
        var row = await _db.CompanySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        return row ?? new CompanySettings
        {
            CompanyId = companyId,
            DebugLoggingEnabled = false,
            ExportDateFormat = ExportDateFormats.Default,
        };
    }

    public async Task SaveAsync(string companyId, CompanySettingsDto settings, string? userId, CancellationToken ct = default)
    {
        var row = await _db.CompanySettings.FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        var now = DateTime.UtcNow;

        // Null = "not being changed"; anything else is normalised so an unsupported pattern can never reach
        // the export path (see ExportDateFormats.Resolve).
        var format = settings.ExportDateFormat == null
            ? row?.ExportDateFormat
            : ExportDateFormats.Resolve(settings.ExportDateFormat);

        if (row == null)
        {
            row = new CompanySettings
            {
                CompanyId = companyId,
                DebugLoggingEnabled = settings.DebugLoggingEnabled,
                ExportDateFormat = format,
                CreatedBy = userId,
                CreatedOn = now,
                ModifiedBy = userId,
                ModifiedOn = now,
            };
            _db.CompanySettings.Add(row);
        }
        else
        {
            row.DebugLoggingEnabled = settings.DebugLoggingEnabled;
            row.ExportDateFormat = format;
            row.ModifiedBy = userId;
            row.ModifiedOn = now;
        }

        await _db.SaveChangesAsync(ct);

        // Refresh both cached reads so a just-saved change takes effect on the next request rather than
        // after the TTL lapses.
        _cache.Set(DebugCacheKey(companyId), row.DebugLoggingEnabled, CacheTtl);
        _cache.Set(ExportFormatCacheKey(companyId), ExportDateFormats.Resolve(row.ExportDateFormat), CacheTtl);
    }

    public Task SetDebugLoggingAsync(string companyId, bool enabled, string? userId, CancellationToken ct = default)
        => SaveAsync(companyId, new CompanySettingsDto { DebugLoggingEnabled = enabled }, userId, ct);

    public async Task<bool> IsDebugLoggingEnabledAsync(string companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return false;
        if (_cache.TryGetValue(DebugCacheKey(companyId), out bool cached))
            return cached;

        var enabled = await _db.CompanySettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.DebugLoggingEnabled)
            .FirstOrDefaultAsync(ct);

        _cache.Set(DebugCacheKey(companyId), enabled, CacheTtl);
        return enabled;
    }

    public async Task<string> GetExportDateFormatAsync(string companyId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return ExportDateFormats.Default;
        if (_cache.TryGetValue(ExportFormatCacheKey(companyId), out string? cached) && cached != null)
            return cached;

        var stored = await _db.CompanySettings.AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => s.ExportDateFormat)
            .FirstOrDefaultAsync(ct);

        var format = ExportDateFormats.Resolve(stored);
        _cache.Set(ExportFormatCacheKey(companyId), format, CacheTtl);
        return format;
    }
}

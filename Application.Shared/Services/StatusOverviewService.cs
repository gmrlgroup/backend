using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class StatusOverviewService : IStatusOverviewService
{
    private readonly StatusDbContext _context;

    public StatusOverviewService(StatusDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<StatusOverviewDto> GetOverviewAsync(string companyId, int days = 30, CancellationToken ct = default)
    {
        if (days < 1) days = 1;
        if (days > 90) days = 90;

        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddDays(-(days - 1));

        // The ordered list of UTC calendar dates we render, oldest -> newest.
        var dateAxis = Enumerable.Range(0, days).Select(i => windowStart.AddDays(i)).ToList();

        var entities = await _context.MonitoredAssets
            .Where(e => e.CompanyId == companyId && e.IsActive && !e.IsDeleted)
            .Select(e => new { e.Id, e.Name, e.EntityType })
            .ToListAsync(ct);

        var entityIds = entities.Select(e => e.Id).ToHashSet();
        var typeById = entities.ToDictionary(e => e.Id, e => e.EntityType);

        // One pass over the window's history; group in memory.
        var history = await _context.AssetStatusHistory
            .Where(h => h.CompanyId == companyId && !h.IsDeleted && h.CheckedAt >= windowStart)
            .Select(h => new { h.EntityId, h.CheckedAt, h.Status })
            .ToListAsync(ct);

        var byEntity = history
            .Where(h => h.EntityId != null && entityIds.Contains(h.EntityId) && h.CheckedAt.HasValue)
            .GroupBy(h => h.EntityId!)
            .ToDictionary(g => g.Key, g => g.OrderBy(h => h.CheckedAt!.Value).ToList());

        var overviewEntities = new List<StatusOverviewEntity>();
        foreach (var e in entities)
        {
            byEntity.TryGetValue(e.Id, out var rows);
            rows ??= new();

            // Last status per UTC day.
            var lastByDay = rows
                .GroupBy(h => h.CheckedAt!.Value.Date)
                .ToDictionary(g => g.Key, g => g.Last().Status);

            var dayCells = dateAxis
                .Select(d => new StatusOverviewDay
                {
                    Date = d,
                    Status = lastByDay.TryGetValue(d, out var s) ? s : (AssetStatus?)null
                })
                .ToList();

            var current = rows.Count > 0 ? rows[^1].Status : AssetStatus.Unknown;

            var daysWithData = dayCells.Count(c => c.Status.HasValue);
            var onlineDays = dayCells.Count(c => c.Status == AssetStatus.Online);
            var uptime = daysWithData > 0 ? Math.Round((double)onlineDays / daysWithData * 100, 1) : 0;

            overviewEntities.Add(new StatusOverviewEntity
            {
                EntityId = e.Id,
                Name = e.Name,
                CurrentStatus = current,
                UptimePercent = uptime,
                Days = dayCells
            });
        }

        var groups = entities
            .Select(e => e.EntityType)
            .Distinct()
            .OrderBy(t => (int)t)
            .Select(type =>
            {
                var members = overviewEntities
                    .Where(m => typeById[m.EntityId] == type)
                    .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new StatusOverviewTypeGroup
                {
                    EntityType = type,
                    TypeLabel = TypeLabel(type),
                    EntityCount = members.Count,
                    WorstCurrentStatus = members.Count > 0
                        ? members.OrderByDescending(m => Severity(m.CurrentStatus)).First().CurrentStatus
                        : AssetStatus.Unknown,
                    Entities = members
                };
            })
            .ToList();

        return new StatusOverviewDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Days = days,
            Groups = groups
        };
    }

    public async Task<List<StatusDayEventDto>> GetDayEventsAsync(string companyId, string entityId, DateTime dateUtc, CancellationToken ct = default)
    {
        var from = dateUtc.Date;
        var to = from.AddDays(1);

        return await _context.AssetStatusHistory
            .Where(h => h.CompanyId == companyId
                        && h.EntityId == entityId
                        && !h.IsDeleted
                        && h.CheckedAt >= from
                        && h.CheckedAt < to)
            .OrderBy(h => h.CheckedAt)
            .Select(h => new StatusDayEventDto
            {
                CheckedAt = h.CheckedAt!.Value,
                Status = h.Status,
                StatusMessage = h.StatusMessage,
                ResponseTime = h.ResponseTime
            })
            .ToListAsync(ct);
    }

    /// <summary>Higher = worse; used to surface the most severe current status.</summary>
    private static int Severity(AssetStatus status) => status switch
    {
        AssetStatus.Offline => 5,
        AssetStatus.Error => 5,
        AssetStatus.Degraded => 4,
        AssetStatus.Maintenance => 3,
        AssetStatus.Unknown => 2,
        AssetStatus.Online => 1,
        _ => 0
    };

    private static string TypeLabel(AssetType type) => type switch
    {
        AssetType.Server => "Servers",
        AssetType.Report => "Reports",
        AssetType.Dataset => "Datasets",
        AssetType.Database => "Databases",
        AssetType.DataPipeline => "Data Pipelines",
        AssetType.Table => "Tables",
        AssetType.DataJob => "Data Jobs",
        _ => type.ToString()
    };
}

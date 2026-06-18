using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Scheduler.Jobs;

/// <summary>
/// Hangfire job that polls all active monitored assets and records their status.
/// </summary>
public class AssetPingJob
{
    private readonly StatusDbContext _context;
    private readonly ILogger<AssetPingJob> _logger;

    public AssetPingJob(StatusDbContext context, ILogger<AssetPingJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunAsync(PerformContext? context, CancellationToken ct = default)
    {
        var entities = await _context.MonitoredAssets
            .Where(e => e.IsActive && !e.IsDeleted)
            .ToListAsync(ct);

        _logger.LogInformation("[AssetPingJob] Checking {Count} active entities", entities.Count);

        // Phase 1: probe all entities in parallel — this is network I/O only and touches no DbContext.
        var probeTasks = entities.Select(async entity => (
            Entity: entity,
            Probe: entity.EntityType == AssetType.Server
                ? await PingHostAsync(entity.Url ?? entity.Name, ct)
                : await CheckHttpAsync(entity.Url, ct)));

        var probes = await Task.WhenAll(probeTasks);

        // Phase 2: persist sequentially. DbContext is not thread-safe, so all reads/writes
        // happen one at a time on the single shared context.
        foreach (var (entity, probe) in probes)
            await PersistResultAsync(entity, probe, ct);

        _logger.LogInformation("[AssetPingJob] Done.");
    }

    private async Task PersistResultAsync(
        MonitoredAsset entity,
        (AssetStatus status, string message, double? responseMs) probe,
        CancellationToken ct)
    {
        var (newStatus, statusMessage, responseTimeMs) = probe;

        var previousStatus = await GetPreviousStatusAsync(entity.Id, ct);

        var history = new AssetStatusHistory
        {
            EntityId = entity.Id,
            Status = newStatus,
            StatusMessage = statusMessage,
            ResponseTime = responseTimeMs,
            CheckedAt = DateTime.UtcNow,
            CompanyId = entity.CompanyId
        };

        _context.AssetStatusHistory.Add(history);

        // Auto-create incident when entity goes from Online → non-Online
        if (previousStatus == AssetStatus.Online && newStatus != AssetStatus.Online)
        {
            var severity = newStatus switch
            {
                AssetStatus.Error => IncidentSeverity.Critical,
                AssetStatus.Offline => entity.IsCritical ? IncidentSeverity.High : IncidentSeverity.Medium,
                AssetStatus.Degraded => IncidentSeverity.Medium,
                AssetStatus.Maintenance => IncidentSeverity.Low,
                _ => IncidentSeverity.Medium
            };

            var incident = new Incident
            {
                EntityId = entity.Id,
                Title = $"{entity.Name} - Status Changed to {newStatus}",
                Description = $"Entity '{entity.Name}' status changed from Online to {newStatus}.\n\nDetails: {statusMessage}",
                Severity = severity,
                Status = IncidentStatus.Open,
                ReportedBy = "AssetPingJob",
                CompanyId = entity.CompanyId,
                ImpactDescription = entity.IsCritical ? "Critical entity affected" : "Service may be impacted"
            };

            _context.Incidents.Add(incident);
            _logger.LogWarning("[AssetPingJob] Incident created for {EntityName}: {Status}", entity.Name, newStatus);
        }
        // Auto-resolve open incidents when entity comes back Online
        else if (previousStatus != AssetStatus.Online && newStatus == AssetStatus.Online)
        {
            var openIncidents = await _context.Incidents
                .Where(i => i.EntityId == entity.Id &&
                            i.Status != IncidentStatus.Resolved &&
                            !i.IsDeleted &&
                            i.CompanyId == entity.CompanyId)
                .ToListAsync(ct);

            foreach (var incident in openIncidents)
            {
                incident.Status = IncidentStatus.Resolved;
                incident.ResolvedAt = DateTime.UtcNow;
                incident.ResolutionDetails = $"Entity restored to Online. {statusMessage}";
                incident.ModifiedOn = DateTime.UtcNow;
            }

            if (openIncidents.Count > 0)
                _logger.LogInformation("[AssetPingJob] Resolved {Count} incidents for {EntityName}", openIncidents.Count, entity.Name);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("[AssetPingJob] {EntityName}: {Status} ({ResponseTime}ms)",
            entity.Name, newStatus, responseTimeMs?.ToString("F0") ?? "N/A");
    }

    private async Task<AssetStatus> GetPreviousStatusAsync(string entityId, CancellationToken ct)
    {
        var latest = await _context.AssetStatusHistory
            .Where(h => h.EntityId == entityId && !h.IsDeleted)
            .OrderByDescending(h => h.CheckedAt)
            .FirstOrDefaultAsync(ct);

        return latest?.Status ?? AssetStatus.Unknown;
    }

    private static async Task<(AssetStatus status, string message, double? responseMs)> CheckHttpAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (AssetStatus.Unknown, "No URL configured", null);

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await httpClient.GetAsync(url, ct);
            sw.Stop();

            var responseMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

            if (response.IsSuccessStatusCode)
                return (AssetStatus.Online, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", responseMs);

            if ((int)response.StatusCode >= 500)
                return (AssetStatus.Error, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", responseMs);

            return (AssetStatus.Degraded, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", responseMs);
        }
        catch (TaskCanceledException)
        {
            return (AssetStatus.Offline, "Request timed out", null);
        }
        catch (Exception ex)
        {
            return (AssetStatus.Offline, ex.Message[..Math.Min(200, ex.Message.Length)], null);
        }
    }

    private static async Task<(AssetStatus status, string message, double? responseMs)> PingHostAsync(string? host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host))
            return (AssetStatus.Unknown, "No host configured", null);

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(host, 3000);
            sw.Stop();

            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return (AssetStatus.Online, $"Ping success — {reply.RoundtripTime}ms", (double)reply.RoundtripTime);

            return (AssetStatus.Offline, $"Ping failed: {reply.Status}", null);
        }
        catch (Exception ex)
        {
            return (AssetStatus.Offline, ex.Message[..Math.Min(200, ex.Message.Length)], null);
        }
    }
}

using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Services;
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
    private readonly IIncidentNotificationService _notification;
    private readonly IDatabaseTableService _databaseService;
    private readonly ILogger<AssetPingJob> _logger;

    public AssetPingJob(StatusDbContext context, IIncidentNotificationService notification,
        IDatabaseTableService databaseService, ILogger<AssetPingJob> logger)
    {
        _context = context;
        _notification = notification;
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task RunAsync(PerformContext? context, CancellationToken ct = default)
    {
        var entities = await _context.MonitoredAssets
            .Where(e => e.IsActive && !e.IsDeleted)
            .ToListAsync(ct);

        _logger.LogInformation("[AssetPingJob] Checking {Count} active entities", entities.Count);

        // Phase 0: resolve each entity's probe plan sequentially. This is the only phase that touches
        // the DbContext (loading + decrypting connections, freshness config, parent-DB resolution),
        // so it must not run concurrently with the parallel probe phase below.
        var plans = new List<ProbePlan>(entities.Count);
        foreach (var entity in entities)
            plans.Add(await BuildPlanAsync(entity, ct));

        // Phase 1: probe all entities in parallel — network/IO only. The DatabaseTableService probe
        // methods used here are pure (no DbContext), so concurrent use is safe.
        var probeTasks = plans.Select(async plan => (Plan: plan, Probe: await ProbeAsync(plan, ct)));
        var probes = await Task.WhenAll(probeTasks);

        // Phase 2: persist sequentially. DbContext is not thread-safe, so all reads/writes
        // happen one at a time on the single shared context.
        foreach (var (plan, probe) in probes)
            await PersistResultAsync(plan.Entity, probe, ct);

        _logger.LogInformation("[AssetPingJob] Done.");
    }

    private enum ProbeKind { Http, Ping, DbConnection, TableFreshness }

    private sealed record ProbePlan(
        MonitoredAsset Entity,
        ProbeKind Kind,
        DatabaseConnection? Connection = null,
        string? TableFullName = null,
        string? FreshnessColumn = null,
        int MaxAgeMinutes = 0);

    /// <summary>Decides how an entity should be probed, loading any DB connection/freshness config it needs.</summary>
    private async Task<ProbePlan> BuildPlanAsync(MonitoredAsset entity, CancellationToken ct)
    {
        var companyId = entity.CompanyId ?? string.Empty;

        switch (entity.EntityType)
        {
            case AssetType.Server:
                return new ProbePlan(entity, ProbeKind.Ping);

            case AssetType.Database:
            {
                // SELECT 1 over the stored read-only connection; fall back to a URL check if none configured.
                var connection = await _databaseService.GetDecryptedConnectionAsync(entity.Id, companyId, ct);
                return connection != null
                    ? new ProbePlan(entity, ProbeKind.DbConnection, connection)
                    : new ProbePlan(entity, ProbeKind.Http);
            }

            case AssetType.Table:
            {
                var check = await _databaseService.GetTableCheckAsync(entity.Id, companyId, ct);
                if (check is { IsEnabled: true } && !string.IsNullOrWhiteSpace(check.FreshnessColumn))
                {
                    var connection = await _databaseService.GetDecryptedParentConnectionAsync(entity.Id, companyId, ct);
                    if (connection != null)
                        return new ProbePlan(entity, ProbeKind.TableFreshness, connection, entity.Name, check.FreshnessColumn, check.MaxAgeMinutes);
                }
                return new ProbePlan(entity, ProbeKind.Http);
            }

            default:
                return new ProbePlan(entity, ProbeKind.Http);
        }
    }

    private async Task<(AssetStatus status, string message, double? responseMs)> ProbeAsync(ProbePlan plan, CancellationToken ct)
    {
        switch (plan.Kind)
        {
            case ProbeKind.Ping:
                return await PingHostAsync(plan.Entity.Url ?? plan.Entity.Name, ct);

            case ProbeKind.DbConnection:
                return MapDbProbe(await _databaseService.ProbeConnectionAsync(plan.Connection!, ct));

            case ProbeKind.TableFreshness:
                return MapFreshness(await _databaseService.CheckFreshnessAsync(
                    plan.Connection!, plan.TableFullName!, plan.FreshnessColumn!, plan.MaxAgeMinutes, ct));

            default:
                return await CheckHttpAsync(plan.Entity.Url, ct);
        }
    }

    private static (AssetStatus status, string message, double? responseMs) MapDbProbe(DatabaseProbeResult r) =>
        r.Ok
            ? (AssetStatus.Online, $"Connection OK — SELECT 1 in {r.ResponseMs:F0}ms", r.ResponseMs)
            : (AssetStatus.Offline, Clip($"Connection failed: {r.Error}"), null);

    private static (AssetStatus status, string message, double? responseMs) MapFreshness(TableFreshnessResult r)
    {
        if (!r.Ok)
            return (AssetStatus.Error, Clip($"Freshness check failed: {r.Error}"), null);

        if (!r.LastUpdatedUtc.HasValue)
            return (AssetStatus.Degraded, Clip($"No timestamp value found ({r.RowCount:N0} rows)"), r.ResponseMs);

        var detail = $"last row {r.LastUpdatedUtc:yyyy-MM-dd HH:mm}Z ({r.AgeMinutes:F0}m ago), {r.RowCount:N0} rows";
        return r.IsStale
            ? (AssetStatus.Degraded, Clip("Stale — " + detail), r.ResponseMs)
            : (AssetStatus.Online, Clip("Fresh — " + detail), r.ResponseMs);
    }

    private static string Clip(string s) => string.IsNullOrEmpty(s) || s.Length <= 200 ? s : s[..200];

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

        Incident? createdIncident = null;

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
            createdIncident = incident;
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

        // Notify the entity's (and upstream entities') audience that an incident was opened.
        if (createdIncident != null)
            await _notification.NotifyIncidentOpenedAsync(createdIncident, ct);

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

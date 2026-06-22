using Application.Shared.Data;
using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Scheduler.Jobs;

/// <summary>
/// Reconciles Hangfire recurring jobs against the <c>ingestion_source</c> table so that sources created,
/// edited, disabled or deleted in the web UI take effect without restarting the scheduler. Runs on a
/// short recurring schedule (and once at startup).
/// </summary>
public class IngestionRegistrarJob
{
    private const string JobPrefix = "ingest-";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<IngestionRegistrarJob> _logger;

    public IngestionRegistrarJob(ApplicationDbContext db, ILogger<IngestionRegistrarJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunAsync(PerformContext? context, CancellationToken ct = default)
    {
        var sources = await _db.IngestionSource.AsNoTracking().ToListAsync(ct);
        var liveJobIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            var jobId = JobPrefix + source.Id;

            if (!source.IsEnabled)
            {
                RecurringJob.RemoveIfExists(jobId);
                continue;
            }

            var tz = ResolveTimeZone(source.TimeZone);
            try
            {
                RecurringJob.AddOrUpdate<ScheduledIngestionJob>(
                    recurringJobId: jobId,
                    methodCall: job => job.RunAsync(source.Id, null, CancellationToken.None),
                    cronExpression: source.CronExpression,
                    timeZone: tz);
                liveJobIds.Add(jobId);
            }
            catch (Exception ex)
            {
                // A bad cron on one source shouldn't break the whole reconcile pass.
                _logger.LogWarning(ex, "Could not schedule ingestion source {SourceId} (cron '{Cron}').",
                    source.Id, source.CronExpression);
            }
        }

        // Remove recurring jobs for sources that were deleted (Hangfire doesn't know about deletions).
        using var connection = JobStorage.Current.GetConnection();
        foreach (var recurring in connection.GetRecurringJobs())
        {
            if (recurring.Id.StartsWith(JobPrefix, StringComparison.Ordinal) && !liveJobIds.Contains(recurring.Id))
                RecurringJob.RemoveIfExists(recurring.Id);
        }
    }

    private static TimeZoneInfo? ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) id = "Asia/Beirut";
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Middle East Standard Time"); }
            catch { return null; }
        }
    }
}

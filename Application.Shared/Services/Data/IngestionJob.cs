using System.Threading;
using System.Threading.Tasks;
using Hangfire.Server;

namespace Application.Shared.Services.Data;

/// <summary>
/// Hangfire entry point for running an ingestion source. Lives in Shared so BOTH the web app (which
/// enqueues batch runs) and the scheduler (which enqueues recurring runs and executes everything) can
/// reference the same job type. It owns the <see cref="PerformContext"/>, turns it into an
/// <see cref="IJobProgress"/>, and delegates the actual work to <see cref="IIngestionService"/> — which
/// stays Hangfire-free. Hangfire injects the PerformContext + CancellationToken at run time.
/// </summary>
public class IngestionJob
{
    private readonly IIngestionService _ingestion;

    public IngestionJob(IIngestionService ingestion)
    {
        _ingestion = ingestion;
    }

    public async Task RunAsync(string sourceId, string? runId, PerformContext? context, CancellationToken ct = default)
    {
        var progress = context != null ? new HangfireJobProgress(context) : null;
        var jobId = context?.BackgroundJob?.Id;
        await _ingestion.RunSourceAsync(sourceId, runId, jobId, progress, ct);
    }
}

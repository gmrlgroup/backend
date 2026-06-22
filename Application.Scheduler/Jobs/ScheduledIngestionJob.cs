using Application.Shared.Services.Data;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace Application.Scheduler.Jobs;

/// <summary>
/// Thin Hangfire wrapper that runs one ingestion source. The actual fetch + load logic lives in the
/// shared <see cref="IIngestionService"/> (also used by the web app's "Run now").
/// </summary>
public class ScheduledIngestionJob
{
    private readonly IIngestionService _ingestion;
    private readonly ILogger<ScheduledIngestionJob> _logger;

    public ScheduledIngestionJob(IIngestionService ingestion, ILogger<ScheduledIngestionJob> logger)
    {
        _ingestion = ingestion;
        _logger = logger;
    }

    public async Task RunAsync(string sourceId, PerformContext? context, CancellationToken ct = default)
    {
        var result = await _ingestion.RunSourceAsync(sourceId, ct);
        if (!result.Success)
            _logger.LogWarning("Ingestion source {SourceId} failed: {Error}", sourceId, result.Error);
        else
            _logger.LogInformation("Ingestion source {SourceId}: {Inserted} inserted, {Updated} updated, {Skipped} skipped",
                sourceId, result.RowsInserted, result.RowsUpdated, result.RowsSkipped);
    }
}

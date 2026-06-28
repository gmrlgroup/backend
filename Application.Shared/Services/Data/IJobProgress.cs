namespace Application.Shared.Services.Data;

/// <summary>
/// Minimal progress/log sink for long-running ingestion work. Kept free of any Hangfire dependency so the
/// executor (<see cref="IngestionService"/>) doesn't couple to Hangfire — the Hangfire-backed
/// implementation lives in <see cref="HangfireJobProgress"/> and is supplied by the job wrapper.
/// </summary>
public interface IJobProgress
{
    /// <summary>Writes a log line (visible in the Hangfire dashboard job console).</summary>
    void WriteLine(string message);

    /// <summary>Sets the job progress bar to a percentage (0–100).</summary>
    void SetProgress(int percent);
}

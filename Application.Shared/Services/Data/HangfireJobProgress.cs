using Hangfire.Console;
using Hangfire.Console.Progress;
using Hangfire.Server;

namespace Application.Shared.Services.Data;

/// <summary>
/// <see cref="IJobProgress"/> backed by Hangfire.Console — writes log lines and drives a progress bar on
/// the dashboard job-details page. Requires the Hangfire server to have <c>UseConsole()</c> configured
/// (the scheduler does). Created by the job wrapper from the current <see cref="PerformContext"/>.
/// </summary>
public sealed class HangfireJobProgress : IJobProgress
{
    private readonly PerformContext _context;
    private readonly IProgressBar _bar;

    public HangfireJobProgress(PerformContext context)
    {
        _context = context;
        _bar = context.WriteProgressBar();
    }

    public void WriteLine(string message) => _context.WriteLine(message);

    public void SetProgress(int percent) => _bar.SetValue(System.Math.Clamp(percent, 0, 100));
}

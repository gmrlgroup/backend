

using Application.Scheduler.Repositories;
using Application.Shared.Models.Data;
using Hangfire.Console;
using Hangfire.Server;


namespace Application.Scheduler.Jobs;


public class SalesJob
{
    private readonly ISalesRepository _repo;
    //private readonly IDatabaseRepository _databaseService;
    private readonly ILogger<SalesJob> _log;

    public SalesJob(ISalesRepository repo, ILogger<SalesJob> log)//, IDatabaseRepository databaseService)
    {
        _repo = repo;
        _log = log;
        //_databaseService = databaseService;
    }

    public async Task RunAsync(Database database, PerformContext context, CancellationToken ct = default)
    {

        var rows = await _repo.GetSalesGroupedByStoreHour(database: database);
        context.WriteLine(rows.Count + " rows fetched at " + DateTime.UtcNow.ToString("O") + ".");

        // TODO: persist rows, push to cache, publish event, etc.
        foreach (var r in rows)
            context.WriteLine(r.StoreCode + " | Hour: " + r.Hour + " | Net: " + r.NetAmountAcy);



    }
}

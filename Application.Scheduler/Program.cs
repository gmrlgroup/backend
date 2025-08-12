using Application.Scheduler.Jobs;
using Application.Scheduler.Repositories;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;




var builder = WebApplication.CreateBuilder(args); // Or use your own Host Builder

var navConnectionString = builder.Configuration.GetConnectionString("NavDbContext") ?? throw new InvalidOperationException("Connection string 'NavDbContext' not found.");
var appConnectionString = builder.Configuration.GetConnectionString("ApplicationDbContext") ?? throw new InvalidOperationException("Connection string 'ApplicationDbContext' not found.");
var connectionString = builder.Configuration.GetConnectionString("SchedulerDbContext") ?? throw new InvalidOperationException("Connection string 'SchedulerDbContext' not found.");


builder.Services.AddDbContext<SchedulerDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly("Application.Scheduler")));


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(appConnectionString, b => b.MigrationsAssembly("Application")));


builder.Services.AddHangfire(cfg => cfg
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseColouredConsoleLogProvider()
    .UseConsole()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "HangFire", // default; change if you want
        PrepareSchemaIfNecessary = true, // auto-create tables on first run
    }));


builder.Services.AddScoped<ISalesRepository, SalesRepository>();
builder.Services.AddScoped<IDatabaseRepository, DatabaseRepository>();
builder.Services.AddScoped<SalesJob>();


builder.Services.AddHangfireServer();

IConfiguration cfg = builder.Configuration;
var _salesUri = cfg.GetValue<string>("SalesApiUri");
builder.Services.AddScoped(http => new HttpClient { BaseAddress = new Uri(_salesUri) });


var app = builder.Build();

// 3) Dashboard (at /hangfire). Add auth if exposed publicly!
app.UseHangfireDashboard("/dashboard");


//// Sample fire-and-forget
//app.MapPost("/enqueue", () =>
//{
//    BackgroundJob.Enqueue(() => Console.WriteLine($"Hello from Hangfire! {DateTime.UtcNow:O}"));
//    return Results.Ok("Enqueued!");
//});

//// Sample recurring (every minute)
//RecurringJob.AddOrUpdate("say-hello",
//    () => Console.WriteLine($"[Recurring] {DateTime.UtcNow:O}"),
//    Cron.Minutely);





// Cron: every 10 minutes
var tz = GetTimeZone("Asia/Beirut"); // or null for server time
#pragma warning disable CS0618 // Type or member is obsolete

List<Database> databases = new List<Database>();

using (var scope = app.Services.CreateScope())
{
    var databaseRepository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
    databases = await databaseRepository.GetDatabaseDetails();
    Console.WriteLine($"Loaded {databases.Count} records at startup");
}


int offset = 0;

foreach (var db in databases)
{

    var minuteOffset = offset % 5; // ensure it’s within 0-9
    //#pragma warning restore CS0618 // Type or member is obsolete
    RecurringJob.AddOrUpdate<SalesJob>(
        recurringJobId: $"sales-grouped-by-store-hour-{db.Name}",
        methodCall: job => job.RunAsync(db, null, CancellationToken.None), // context and ct are not used in this example
        cronExpression: $"{minuteOffset}/5 * * * *",
        timeZone: tz
    );

    offset++;
}



app.Run();



static TimeZoneInfo? GetTimeZone(string id)
{
    try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
    catch
    { /* Windows/Linux name differences */
        try { return TimeZoneInfo.FindSystemTimeZoneById("Middle East Standard Time"); } // Windows for Beirut
        catch { return null; } // fall back to server local time
    }
}
using System.Net.Http.Headers;
using Application.Scheduler.Jobs;
using Application.Scheduler.Options;
using Application.Scheduler.Repositories;
using Application.Scheduler.Services;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;




var builder = WebApplication.CreateBuilder(args); // Or use your own Host Builder

var navConnectionString = builder.Configuration.GetConnectionString("NavDbContext") ?? throw new InvalidOperationException("Connection string 'NavDbContext' not found.");
var appConnectionString = builder.Configuration.GetConnectionString("ApplicationDbContext") ?? throw new InvalidOperationException("Connection string 'ApplicationDbContext' not found.");
var connectionString = builder.Configuration.GetConnectionString("SchedulerDbContext") ?? throw new InvalidOperationException("Connection string 'SchedulerDbContext' not found.");


// get the configuration from appsettings.json
var configuration = builder.Configuration;

// get d365 url from appsettings.json
var d365Url = configuration[$"NokNok_D365:ApiUri"];


// get the app registration details from app settings
var aadTenant = configuration[$"NokNok_D365:AppRegistration:Tenant"];
var aadResource = configuration[$"NokNok_D365:AppRegistration:Resource"];
var aadClientAppId = configuration[$"NokNok_D365:AppRegistration:ClientId"];
var aadClientAppSecret = configuration[$"NokNok_D365:AppRegistration:ClientSecret"];

// Create HttpClient service
builder.Services.AddHttpClient("NokNok_D365Api", client => 
{
    client.BaseAddress = new Uri(d365Url);
    client.Timeout = TimeSpan.FromMinutes(10); // Increase timeout to 10 minutes

    // get token
    var authenticationService = new AuthenticationService(aadTenant, aadResource, aadClientAppId, aadClientAppSecret);
    var token = authenticationService.GetAuthenticationHeader();

    // add authentication to the header
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("NokNok_D365Api"));

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
builder.Services.AddScoped<SalesSnapshotEmailJob>();

builder.Services.Configure<SalesSnapshotEmailOptions>(builder.Configuration.GetSection("SalesSnapshotEmail"));

builder.Services.AddHttpClient("SalesSnapshotEmailApi", (sp, client) =>
{
    var emailOptions = sp.GetRequiredService<IOptions<SalesSnapshotEmailOptions>>().Value;
    if (string.IsNullOrWhiteSpace(emailOptions.ApiBaseUri))
    {
        return;
    }

    client.BaseAddress = new Uri(emailOptions.ApiBaseUri);
    client.Timeout = TimeSpan.FromSeconds(60);
});


builder.Services.AddHangfireServer();

IConfiguration cfg = builder.Configuration;
var _salesUri = cfg.GetValue<string>("SalesApiUri");
builder.Services.AddScoped(http => new HttpClient { BaseAddress = new Uri(_salesUri) });


var app = builder.Build();

// 3) Dashboard (at /hangfire). Add auth if exposed publicly!
app.UseHangfireDashboard("/dashboard");




// Cron: every 10 minutes
var tz = GetTimeZone("Asia/Beirut"); // or null for server time
// #pragma warning disable CS0618 // Type or member is obsolete

List<Database> databases = new List<Database>();
List<Database> noknokDatabases = new List<Database>();

using (var scope = app.Services.CreateScope())
{
    var databaseRepository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
    databases = await databaseRepository.GetDatabaseDetails();
    Console.WriteLine($"Loaded {databases.Count} records at startup from RBO");
}

// using (var scope = app.Services.CreateScope())
// {
//     var databaseRepository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository>();
//     noknokDatabases = await databaseRepository.GetNokNokDatabaseDetails();
//     Console.WriteLine($"Loaded {noknokDatabases.Count} records at startup from NKDB");
// }



int offset = 0;
var minuteOffset = 0;


foreach (var db in databases)
{

    minuteOffset = offset % 10; // ensure it’s within 0-9
    //#pragma warning restore CS0618 // Type or member is obsolete
    RecurringJob.AddOrUpdate<SalesJob>(
        recurringJobId: $"sales-grouped-by-store-hour-{db.Name}",
        methodCall: job => job.RunAsync(db, null, CancellationToken.None), // context and ct are not used in this example
        cronExpression: $"{minuteOffset}/10 * * * *",
        timeZone: tz
    );

    offset++;
}


// foreach (var db in noknokDatabases)
// {

//     var minuteOffset = offset % 5; // ensure it’s within 0-9
//     //#pragma warning restore CS0618 // Type or member is obsolete
//     RecurringJob.AddOrUpdate<SalesJob>(
//         recurringJobId: $"sales-grouped-by-store-hour-NOKNOK", //{db.Name}
//         methodCall: job => job.RunAsync(db, null, CancellationToken.None), // context and ct are not used in this example
//         cronExpression: $"{minuteOffset}/5 * * * *",
//         timeZone: tz
//     );

//     offset++;
// }


minuteOffset = 0; // ensure it’s within 0-9
//#pragma warning restore CS0618 // Type or member is obsolete
RecurringJob.AddOrUpdate<SalesJob>(
    recurringJobId: $"sales-grouped-by-store-hour_FO", //{db.Name}
    methodCall: job => job.RunNokNokFoAsync(null, CancellationToken.None), // context and ct are not used in this example
    cronExpression: $"{minuteOffset}/15 * * * *",
    timeZone: tz
);

RecurringJob.AddOrUpdate<SalesSnapshotEmailJob>(
    recurringJobId: "sales-snapshot-email",
    methodCall: job => job.RunAsync(CancellationToken.None),
    cronExpression: "5 0 * * *",
    timeZone: tz
);


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
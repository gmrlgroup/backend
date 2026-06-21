using System.Net.Http.Headers;
using Application.Scheduler.Jobs;
using Application.Scheduler.Options;
using Application.Scheduler.Repositories;
using Application.Scheduler.Services;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Microsoft.AspNetCore.DataProtection;
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

var statusConnectionString = builder.Configuration.GetConnectionString("StatusDbContext") ?? appConnectionString;
builder.Services.AddDbContext<StatusDbContext>(options =>
    options.UseSqlServer(statusConnectionString, b => b.MigrationsAssembly("Application")));

// Data Protection — MUST mirror the web app (same application name + key ring + DPAPI scope)
// so the scheduler can decrypt connection secrets the web app encrypted. AssetPingJob uses this
// to read stored DatabaseConnection passwords for read-only SELECT 1 / freshness probes.
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(keysPath);
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("FlowByte.Application")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
if (OperatingSystem.IsWindows())
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddSingleton<Application.Shared.Services.ICredentialProtector,
    Application.Shared.Services.CredentialProtector>();
builder.Services.AddScoped<Application.Shared.Services.IDatabaseTableService,
    Application.Shared.Services.DatabaseTableService>();
builder.Services.AddHttpClient();


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
builder.Services.AddScoped<AssetPingJob>();

// Incident notification emails (Resend microservice) — used by AssetPingJob auto-incidents.
builder.Services.Configure<Application.Shared.Options.IncidentEmailOptions>(
    builder.Configuration.GetSection("IncidentNotificationEmail"));
builder.Services.AddScoped<Application.Shared.Services.IIncidentNotificationService,
    Application.Shared.Services.IncidentNotificationService>();
builder.Services.AddHttpClient(Application.Shared.Services.IncidentNotificationService.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<Application.Shared.Options.IncidentEmailOptions>>().Value;
    if (string.IsNullOrWhiteSpace(opts.ApiBaseUri)) return;
    client.BaseAddress = new Uri(opts.ApiBaseUri);
    client.Timeout = TimeSpan.FromSeconds(60);
});

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

    minuteOffset = offset % 5; // ensure it’s within 0-9
    //#pragma warning restore CS0618 // Type or member is obsolete
    RecurringJob.AddOrUpdate<SalesJob>(
        recurringJobId: $"sales-grouped-by-store-hour-{db.Name}",
        methodCall: job => job.RunAsync(db, null, CancellationToken.None), // context and ct are not used in this example
        cronExpression: $"{minuteOffset}/5 * * * *",
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

// Status ping monitoring — every 15 minutes
RecurringJob.AddOrUpdate<AssetPingJob>(
    recurringJobId: "asset-ping-monitoring",
    methodCall: job => job.RunAsync(null, CancellationToken.None),
    cronExpression: "*/15 * * * *",
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
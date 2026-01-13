using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Scheduler.Options;
using Application.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Scheduler.Jobs;

public class SalesSnapshotEmailJob
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SalesSnapshotEmailJob> _logger;
    private readonly SalesSnapshotEmailOptions _options;

    public SalesSnapshotEmailJob(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IOptions<SalesSnapshotEmailOptions> options,
        ILogger<SalesSnapshotEmailJob> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Sales snapshot email job skipped because configuration is incomplete.");
            return;
        }

        // var timeZone = ResolveTimeZone(_options.TimeZoneId);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Middle East Standard Time");
        var localNow = ConvertToTimeZone(DateTime.UtcNow, timeZone);
        var snapshotDateLocal = localNow.Date.AddDays(-1);
        var (startUtc, endUtc) = GetUtcWindow(snapshotDateLocal, timeZone);


        var latestPerKey = _context.SalesData
                // .Where(sd => sd.CompanyId == _options.CompanyId && sd.ReceivedAt.Date == snapshotDateLocal.Date)
                .Where(sd => sd.CompanyId == _options.CompanyId 
                            && sd.ReceivedAt >= startUtc 
                            && sd.ReceivedAt < endUtc)
                .GroupBy(sd => new { sd.StoreCode, sd.Scheme, sd.DivisionName, sd.CategoryName })
                .Select(g => new
                {
                    g.Key.StoreCode,
                    g.Key.Scheme,
                    g.Key.DivisionName,
                    g.Key.CategoryName,
                    ReceivedAt = g.Max(x => x.ReceivedAt)   // latest per (Store, Scheme)
                });

        var storeSummaries = await _context.SalesData
            .Where(sd => sd.CompanyId == _options.CompanyId)
            .Join(
                latestPerKey,
                sd => new { sd.StoreCode, sd.Scheme, sd.DivisionName, sd.CategoryName, sd.ReceivedAt },
                l => new { l.StoreCode, l.Scheme, l.DivisionName, l.CategoryName, l.ReceivedAt },
                (sd, l) => new StoreSnapshot
                {
                    Scheme = sd.Scheme,
                    StoreCode = sd.StoreCode,
                    DivisionName = sd.DivisionName,
                    CategoryName = sd.CategoryName,
                    TotalSales = sd.NetAmountAcy,
                    TotalTransactions = Convert.ToInt32(sd.TotalStoreTransactions ?? 0),
                    LastUpdatedUtc = sd.ReceivedAt
                })
            .OrderByDescending(s => s.Scheme).ThenByDescending(s => s.TotalSales)
            .ToListAsync();

        var culture = GetCulture(_options.CurrencyCulture);
        var totalSales = storeSummaries.Sum(s => s.TotalSales);
        var totalTransactions = storeSummaries.Sum(s => s.TotalTransactions);
        var storeCount = storeSummaries.Select(s => s.StoreCode).Distinct().Count();
        var schemeCount = storeSummaries.Select(s => s.Scheme).Distinct().Count();
        var lastUpdatedUtc = storeSummaries.Max(s => s.LastUpdatedUtc);
        var lastUpdatedLocal = ConvertToTimeZone(lastUpdatedUtc, timeZone);

        var totalKpis = new List<MetricCard>
        {
            new()
            {
                Label = "Total Sales",
                Value = FormatCurrency(totalSales, culture),
                HelperText = $"{storeCount} stores / {schemeCount} schemes"
            },
            new()
            {
                Label = "Transactions",
                Value = totalTransactions.ToString("N0", culture),
                HelperText = "Snapshot total"
            },
            new()
            {
                Label = "Average Basket",
                Value = totalTransactions > 0 ? FormatCurrency(totalSales / totalTransactions, culture) : "0",
                HelperText = "Net / transactions"
            }
        };

        var schemeKpis = storeSummaries
            .GroupBy(s => s.Scheme ?? "Unknown")
            .Select(g => new
            {
                Scheme = g.Key,
                TotalSales = g.Sum(x => x.TotalSales),
                TotalTransactions = g.Sum(x => x.TotalTransactions),
                Stores = g.Select(x => x.StoreCode).Distinct().Count()
            })
            .OrderByDescending(s => s.TotalSales)
            .Select(s => new SchemeKpiCard
            {
                SchemeName = s.Scheme,
                TotalSales = FormatCurrency(s.TotalSales, culture),
                Stores = s.Stores,
                Transactions = s.TotalTransactions
            })
            .ToList();

        var columns = new List<string>
        {
            "Store",
            "Scheme",
            "Last Hour",
            "Net Amount",
            "Transactions",
            "Avg Basket"
        };

        // group by store and scheme, take latest hour without category and division
        var rows = storeSummaries
        
        .GroupBy(s => new { s.StoreCode, s.Scheme })
        .Select(g =>
        {
            var maxHour = g.Max(x => x.Hour);
            var hourRows = g.Where(x => x.Hour == maxHour).ToList();
            var latestRow = hourRows
                .OrderByDescending(x => x.LastUpdatedUtc)
                .First();

            return new StoreSnapshot
            {
                Scheme = latestRow.Scheme ?? "N/A",
                StoreCode = latestRow.StoreCode ?? "N/A",
                Hour = maxHour,
                TotalSales = hourRows.Sum(x => x.TotalSales),
                TotalTransactions = hourRows.Sum(x => x.TotalTransactions),
                LastUpdatedUtc = hourRows.Max(x => x.LastUpdatedUtc)
            };
        })
        .Select(store => new DataTableRowPayload
        {
            Data = new Dictionary<string, object?>
            {
                ["Store"] = store.StoreCode,
                ["Scheme"] = store.Scheme,
                ["Last Hour"] = FormatHour(store.Hour),
                ["Net Amount"] = FormatCurrency(store.TotalSales, culture),
                ["Transactions"] = store.TotalTransactions.ToString("N0", culture),
                ["Avg Basket"] = store.TotalTransactions > 0 ? FormatCurrency(store.TotalSales / store.TotalTransactions, culture) : "0"
            }
        }).ToList();

        var payload = new SalesSnapshotEmailPayload
        {
            From = _options.From!,
            To = _options.Recipients,
            RecipientName = _options.RecipientName,
            Title = BuildTitle(snapshotDateLocal),
            Description = BuildDescription(snapshotDateLocal),
            LastUpdatedLabel = $"{snapshotDateLocal:dddd, MMM dd} • {lastUpdatedLocal:HH:mm} {(timeZone?.StandardName ?? "UTC")}",
            TotalKpis = totalKpis,
            SchemeKpis = schemeKpis,
            Columns = columns,
            Rows = rows
        };

        await SendEmailAsync(payload, cancellationToken);
    }

    private async Task SendEmailAsync(SalesSnapshotEmailPayload payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("SalesSnapshotEmailApi");
        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint) ? "/api/email/sales/snapshot" : _options.Endpoint;

        try
        {
            var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Sales snapshot email sent successfully for {Title}", payload.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send sales snapshot email for {Title}", payload.Title);
            throw;
        }
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.ApiBaseUri)
        && !string.IsNullOrWhiteSpace(_options.From)
        && _options.Recipients is { Count: > 0 };

    private static TimeZoneInfo? ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime ConvertToTimeZone(DateTime utcValue, TimeZoneInfo? timeZone)
    {
        if (timeZone == null)
        {
            return DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcValue, DateTimeKind.Utc), timeZone);
    }

    private static (DateTime startUtc, DateTime endUtc) GetUtcWindow(DateTime localDate, TimeZoneInfo? timeZone)
    {
        var startLocal = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(localDate.AddDays(1), DateTimeKind.Unspecified);

        if (timeZone == null)
        {
            return (startLocal, endLocal);
        }

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone)
        );
    }

    private CultureInfo GetCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static string FormatCurrency(decimal value, CultureInfo culture) => value.ToString("N0", culture);

    private static string FormatHour(int hour) => $"{Math.Clamp(hour, 0, 23):00}:00";

    private string BuildTitle(DateTime snapshotDate) =>
        FormatTemplate(_options.Title, snapshotDate) ?? $"Daily Sales Snapshot · {snapshotDate:MMM dd}";

    private string BuildDescription(DateTime snapshotDate) =>
        FormatTemplate(_options.Description, snapshotDate) ?? $"Auto-generated summary for {snapshotDate:dddd, MMM dd}";

    private static string? FormatTemplate(string? template, DateTime snapshotDate)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(CultureInfo.InvariantCulture, template, snapshotDate)
            : template;
    }

    private sealed class StoreSnapshot
    {
        public string Scheme { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string? DivisionName { get; set; }
        public string? CategoryName { get; set; }
        public int Hour { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalTransactions { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    private sealed class SalesSnapshotEmailPayload
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public List<string> To { get; set; } = new();

        [JsonPropertyName("recipientName")]
        public string? RecipientName { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("lastUpdatedLabel")]
        public string? LastUpdatedLabel { get; set; }

        [JsonPropertyName("totalKpis")]
        public List<MetricCard> TotalKpis { get; set; } = new();

        [JsonPropertyName("schemeKpis")]
        public List<SchemeKpiCard> SchemeKpis { get; set; } = new();

        [JsonPropertyName("Columns")]
        public List<string> Columns { get; set; } = new();

        [JsonPropertyName("Rows")]
        public List<DataTableRowPayload> Rows { get; set; } = new();
    }

    private sealed class MetricCard
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("changeLabel")]
        public string? ChangeLabel { get; set; }

        [JsonPropertyName("changeDirection")]
        public string? ChangeDirection { get; set; }

        [JsonPropertyName("helperText")]
        public string? HelperText { get; set; }
    }

    private sealed class SchemeKpiCard
    {
        [JsonPropertyName("schemeName")]
        public string SchemeName { get; set; } = string.Empty;

        [JsonPropertyName("totalSales")]
        public string TotalSales { get; set; } = string.Empty;

        [JsonPropertyName("deltaLabel")]
        public string? DeltaLabel { get; set; }

        [JsonPropertyName("changeDirection")]
        public string? ChangeDirection { get; set; }

        [JsonPropertyName("stores")]
        public int? Stores { get; set; }

        [JsonPropertyName("transactions")]
        public int? Transactions { get; set; }
    }

    private sealed class DataTableRowPayload
    {
        [JsonPropertyName("Data")]
        public Dictionary<string, object?> Data { get; set; } = new();
    }
}

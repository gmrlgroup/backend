using System.Globalization;
using System.Text.RegularExpressions;
using Application.Shared.Models.Dashboards;
using Application.Shared.Models.Dashboards.Oos;
using Application.Shared.Services.Data;

namespace Application.Dashboard.Services;

/// <summary>
/// Builds the Out-of-Stock dashboard entirely from the <b>pre-computed itemized table</b> that a scheduled
/// ingestion writes into a DuckDB dataset (connected via <see cref="DashboardDataLink"/>). The dashboard
/// touches ClickHouse <b>zero times</b> — the heavy aggregation runs once per ingestion; the dashboard
/// only aggregates a few thousand local rows, so it loads in well under a second.
///
/// The ingested table must carry the columns produced by the OOS ingestion query:
///   company_id, item_no, variant_code, product, category, location_code, store, region, warehouse,
///   store_group, supplier, on_hand, velocity, price, state, days_out, lost,
///   fill_rate, total_catalog, total_stores, as_of_date.
/// </summary>
public class OosDashboardService : IOosDashboardService
{
    private readonly IDashboardLinkService _links;
    private readonly IDuckdbService _duckdb;

    // Guards string-interpolated parameters against injection (header/route/link supplied values).
    private static readonly Regex SafeToken = new("^[A-Za-z0-9_\\-]+$", RegexOptions.Compiled);
    private static readonly Regex SafeIdentifier = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public OosDashboardService(IDashboardLinkService links, IDuckdbService duckdb)
    {
        _links = links;
        _duckdb = duckdb;
    }

    public async Task<OosDashboardResponse> GetAsync(string companyId, DateTime asOf, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(companyId) || !SafeToken.IsMatch(companyId))
            throw new ArgumentException("Invalid company id.", nameof(companyId));

        var response = new OosDashboardResponse { AsOfDate = asOf };

        // Resolve the ingested table linked to this dashboard. No link ⇒ nothing to show yet.
        var link = await _links.GetAsync(companyId, DashboardPages.Oos, ct);
        if (link == null || string.IsNullOrWhiteSpace(link.DatasetId) || string.IsNullOrWhiteSpace(link.TableName))
            return response;

        if (!SafeIdentifier.IsMatch(link.TableName))
            return response; // unexpected table name — refuse to interpolate it
        var table = $"\"{link.TableName}\"";
        var co = companyId.Replace("'", "''");

        // 1) Latest snapshot → affected items + per-vendor fill rate + catalog/store denominators.
        var itemsSql = $@"
            SELECT category, region, warehouse, store_group, store, supplier, state,
                   CAST(days_out AS BIGINT) AS days_out,
                   CAST(lost AS DOUBLE) AS lost,
                   CAST(fill_rate AS DOUBLE) AS fill_rate,
                   CAST(total_catalog AS BIGINT) AS total_catalog,
                   CAST(total_stores AS BIGINT) AS total_stores
            FROM {table}
            WHERE company_id = '{co}'
              AND as_of_date = (SELECT max(as_of_date) FROM {table} WHERE company_id = '{co}')";
        var itemsRes = await _duckdb.ExecuteSqlAsync(link.DatasetId, itemsSql, allowWrite: false, maxRows: 200000, ct);
        if (string.IsNullOrEmpty(itemsRes.Error))
        {
            foreach (var r in itemsRes.Rows)
            {
                if (response.TotalCatalog == 0) response.TotalCatalog = (int)GetLong(r, "total_catalog");
                if (response.TotalStores == 0) response.TotalStores = (int)GetLong(r, "total_stores");

                var supplier = GetString(r, "supplier");
                if (!string.IsNullOrEmpty(supplier) && !response.VendorFillRates.ContainsKey(supplier))
                    response.VendorFillRates[supplier] = GetDouble(r, "fill_rate");

                response.Items.Add(new OosItem
                {
                    Category = GetString(r, "category"),
                    Region = GetString(r, "region"),
                    Warehouse = GetString(r, "warehouse"),
                    Group = GetString(r, "store_group"),
                    Store = GetString(r, "store"),
                    Supplier = supplier,
                    State = GetString(r, "state"),
                    DaysOut = (int)GetLong(r, "days_out"),
                    Lost = GetDouble(r, "lost")
                });
            }
        }

        // 2) Trend — OOS rate per month (YTD vs prior year) from however many snapshots exist in the table.
        //    Per month we take the average daily OOS count; a single snapshot yields one point.
        var trendSql = $@"
            SELECT y, mo, avg(c) AS oos FROM (
                SELECT CAST(year(CAST(as_of_date AS DATE)) AS BIGINT) AS y,
                       CAST(month(CAST(as_of_date AS DATE)) AS BIGINT) AS mo,
                       as_of_date, count(*) AS c
                FROM {table}
                WHERE company_id = '{co}'
                GROUP BY as_of_date, year(CAST(as_of_date AS DATE)), month(CAST(as_of_date AS DATE))
            ) GROUP BY y, mo ORDER BY y, mo";
        var byYm = new Dictionary<(int, int), double>();
        var trendRes = await _duckdb.ExecuteSqlAsync(link.DatasetId, trendSql, allowWrite: false, maxRows: 1000, ct);
        if (string.IsNullOrEmpty(trendRes.Error))
            foreach (var t in trendRes.Rows)
                byYm[((int)GetLong(t, "y"), (int)GetLong(t, "mo"))] = GetDouble(t, "oos");

        double denom = response.TotalCatalog > 0 ? response.TotalCatalog : 1;
        for (var mo = 1; mo <= 12; mo++)
        {
            double? thisYear = mo <= asOf.Month
                ? Math.Round((byYm.TryGetValue((asOf.Year, mo), out var c) ? c : 0) / denom * 100, 2)
                : null;
            double priorYear = Math.Round((byYm.TryGetValue((asOf.Year - 1, mo), out var p) ? p : 0) / denom * 100, 2);

            response.Trend.Add(new OosTrendPoint
            {
                Month = mo,
                Label = new DateTime(2000, mo, 1).ToString("MMM", CultureInfo.GetCultureInfo("en-US")),
                ThisYear = thisYear,
                PriorYear = priorYear
            });
        }

        // 3) Deep-link to the linked dataset table for item-level detail.
        response.DetailsUrl = $"/data/view?DatasetId={Uri.EscapeDataString(link.DatasetId)}&TableName={Uri.EscapeDataString(link.TableName)}";

        return response;
    }

    // DuckDB returns native types; these helpers accept long / double / string transparently.
    private static string GetString(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v != null ? v.ToString() ?? string.Empty : string.Empty;

    private static double GetDouble(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v == null) return 0;
        return v switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            long l => l,
            int i => i,
            _ => double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0
        };
    }

    private static long GetLong(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v == null) return 0;
        return v switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            decimal m => (long)m,
            _ => long.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0
        };
    }
}

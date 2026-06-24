using Application.DailyInventory.Configuration;
using Application.Shared.Models.DailyInventory;
using ClickHouse.Client.ADO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace Application.DailyInventory.Services;

public class DailyInventoryService : IDailyInventoryService
{
    private readonly ClickHouseSettings _settings;
    private readonly ILogger<DailyInventoryService> _logger;

    // Key columns of item_details that are never offered as selectable detail columns.
    private static readonly HashSet<string> KeyColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "company_id", "item_no", "variant_code"
    };

    public DailyInventoryService(IOptions<ClickHouseSettings> settings, ILogger<DailyInventoryService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<DailyInventoryRow>> GetDailyInventoryAsync(DailyInventoryQuery query, CancellationToken cancellationToken = default)
    {
        var end = query.EndDate.Date;
        var w1Start = end.AddDays(-7);
        var w2Start = end.AddDays(-14);
        var w3Start = end.AddDays(-21);
        var w4Start = end.AddDays(-28);
        var w5Start = end.AddDays(-35);
        var w6Start = end.AddDays(-42);

        var sql = BuildQuery(query, end, w1Start, w2Start, w3Start, w4Start, w5Start, w6Start);

        _logger.LogInformation("Daily inventory query (company {CompanyId}, end {EndDate:yyyy-MM-dd}):\n{Sql}",
            query.CompanyId, end, sql);

        using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var rows = new List<DailyInventoryRow>();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new DailyInventoryRow
            {
                ItemNo       = reader.GetValue(0) as string,
                VariantCode  = reader.GetValue(1) as string,
                LocationCode = reader.GetValue(2) as string,
                StockOnHand  = ToDouble(reader.GetValue(3)),
                Week1Qty     = ToDouble(reader.GetValue(4)),
                Week2Qty     = ToDouble(reader.GetValue(5)),
                Week3Qty     = ToDouble(reader.GetValue(6)),
                Week4Qty     = ToDouble(reader.GetValue(7)),
                Week5Qty     = ToDouble(reader.GetValue(8)),
                Week6Qty     = ToDouble(reader.GetValue(9)),
            });
        }

        return rows;
    }

    // The main query intentionally does NOT touch daily_inventory.item_details — item attributes are
    // fetched on demand via GetItemDetailsAsync and joined client-side only for the columns the user picks.
    private static string BuildQuery(DailyInventoryQuery q, DateTime end,
        DateTime w1Start, DateTime w2Start, DateTime w3Start,
        DateTime w4Start, DateTime w5Start, DateTime w6Start)
    {
        var companyId = EscapeString(q.CompanyId ?? "");

        // item_location filters (applied before aggregation).
        var ilFilters = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(q.ItemNo))
            ilFilters.Append($"      AND lower(il.item_no) LIKE lower('%{EscapeString(q.ItemNo)}%')\n");
        if (!string.IsNullOrWhiteSpace(q.VariantCode))
            ilFilters.Append($"      AND lower(il.variant_code) LIKE lower('%{EscapeString(q.VariantCode)}%')\n");
        if (q.LocationCodes is { Count: > 0 })
        {
            var inList = string.Join(",", q.LocationCodes.Select(c => $"'{EscapeString(c)}'"));
            ilFilters.Append($"      AND il.location_code IN ({inList})\n");
        }

        // The per-item aggregates (stock-on-hand + the 6 rolling weekly sales sums).
        var aggregates = $@"
                        sumIf(t.quantity, toDate(t.posting_date) <= toDate('{end:yyyy-MM-dd}'))
                            AS stock_on_hand,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w1Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <= toDate('{end:yyyy-MM-dd}'))
                            AS week1_qty,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w2Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <  toDate('{w1Start:yyyy-MM-dd}'))
                            AS week2_qty,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w3Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <  toDate('{w2Start:yyyy-MM-dd}'))
                            AS week3_qty,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w4Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <  toDate('{w3Start:yyyy-MM-dd}'))
                            AS week4_qty,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w5Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <  toDate('{w4Start:yyyy-MM-dd}'))
                            AS week5_qty,
                        sumIf(t.quantity, t.entry_type = 'SALES'
                            AND toDate(t.posting_date) >= toDate('{w6Start:yyyy-MM-dd}')
                            AND toDate(t.posting_date) <  toDate('{w5Start:yyyy-MM-dd}'))
                            AS week6_qty";

        var txnJoin = $@"
                    LEFT JOIN (
                        SELECT
                            company_id, item_no, variant_code, location_code,
                            quantity, entry_type, posting_date
                        FROM daily_inventory.transaction
                        WHERE company_id = '{companyId}'
                        AND toDate(posting_date) <= toDate('{end:yyyy-MM-dd}')
                    ) AS t
                        ON  il.company_id    = t.company_id
                        AND il.item_no       = t.item_no
                        AND il.variant_code  = t.variant_code
                        AND il.location_code = t.location_code";

        // "Has stock" / "has sales" filter on aggregates. When neither is set we keep the fast path that
        // paginates item_location FIRST and only aggregates that page. When set we must aggregate the whole
        // (il-filtered) set, filter on the aggregates, THEN paginate — otherwise LIMIT/OFFSET would slice
        // before the aggregate filter and pagination would be wrong.
        if (!q.HasStock && !q.HasSales)
        {
            return $@"
                    WITH top_il AS (
                        SELECT
                            il.company_id,
                            il.item_no,
                            il.variant_code,
                            il.location_code
                        FROM daily_inventory.item_location AS il
                        WHERE il.company_id = '{companyId}'
{ilFilters}
                        ORDER BY il.item_no, il.variant_code, il.location_code
                        LIMIT {q.Limit} OFFSET {q.Offset}
                    )
                    SELECT
                        il.item_no,
                        il.variant_code,
                        il.location_code,
{aggregates}
                    FROM top_il AS il
{txnJoin}
                    GROUP BY
                        il.item_no, il.variant_code, il.location_code
                    ORDER BY il.item_no, il.variant_code, il.location_code
                    ";
        }

        var having = new StringBuilder();
        if (q.HasStock) having.Append("      AND stock_on_hand > 0\n");
        if (q.HasSales) having.Append("      AND (week1_qty <> 0 OR week2_qty <> 0 OR week3_qty <> 0 OR week4_qty <> 0 OR week5_qty <> 0 OR week6_qty <> 0)\n");

        return $@"
                    SELECT
                        il.item_no,
                        il.variant_code,
                        il.location_code,
{aggregates}
                    FROM daily_inventory.item_location AS il
{txnJoin}
                    WHERE il.company_id = '{companyId}'
{ilFilters}
                    GROUP BY il.item_no, il.variant_code, il.location_code
                    HAVING 1 = 1
{having}
                    ORDER BY il.item_no, il.variant_code, il.location_code
                    LIMIT {q.Limit} OFFSET {q.Offset}
                    ";
    }

    public async Task<List<string>> GetDetailColumnsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "DESCRIBE daily_inventory.item_details";

        using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var columns = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // DESCRIBE returns: name, type, default_type, ... — the column name is field 0.
            var name = reader.GetValue(0) as string;
            if (!string.IsNullOrWhiteSpace(name) && !KeyColumns.Contains(name))
                columns.Add(name);
        }

        return columns;
    }

    public async Task<List<ItemDetailsRow>> GetItemDetailsAsync(string companyId, List<string> columns, List<ItemKey> items, CancellationToken cancellationToken = default)
    {
        if (columns is not { Count: > 0 } || items is not { Count: > 0 })
            return new();

        // Only allow columns that actually exist in item_details (prevents identifier injection).
        var allowed = (await GetDetailColumnsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var safeColumns = columns.Where(c => allowed.Contains(c)).Distinct().ToList();
        if (safeColumns.Count == 0)
            return new();

        var escapedCompanyId = EscapeString(companyId);
        var selectCols = string.Join(", ", safeColumns.Select(Q));
        var tuples = string.Join(",", items.Select(i =>
            $"('{EscapeString(i.ItemNo ?? "")}','{EscapeString(i.VariantCode ?? "")}')"));

        var sql = $@"
            SELECT item_no, variant_code, {selectCols}
            FROM daily_inventory.item_details
            WHERE company_id = '{escapedCompanyId}'
              AND (item_no, variant_code) IN ({tuples})";

        _logger.LogInformation("Item details query (company {CompanyId}, {ColumnCount} cols, {ItemCount} items)",
            companyId, safeColumns.Count, items.Count);

        using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<ItemDetailsRow>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new ItemDetailsRow
            {
                ItemNo = reader.GetValue(0) as string,
                VariantCode = reader.GetValue(1) as string
            };
            for (var i = 0; i < safeColumns.Count; i++)
                row.Values[safeColumns[i]] = ToStringValue(reader.GetValue(2 + i));
            results.Add(row);
        }

        return results;
    }

    private const int ExportRowCap = 1_000_000;

    public async Task ExportCsvAsync(DailyInventoryQuery query, List<string> detailColumns, Stream output, CancellationToken cancellationToken = default)
    {
        var end = query.EndDate.Date;
        var w1Start = end.AddDays(-7);
        var w2Start = end.AddDays(-14);
        var w3Start = end.AddDays(-21);
        var w4Start = end.AddDays(-28);
        var w5Start = end.AddDays(-35);
        var w6Start = end.AddDays(-42);

        // Same filtered query as the grid, but no offset and capped at the first ExportRowCap rows.
        query.Offset = 0;
        query.Limit = ExportRowCap;
        var baseSql = BuildQuery(query, end, w1Start, w2Start, w3Start, w4Start, w5Start, w6Start);

        // Validate any requested item_details columns against the real schema (prevents identifier injection).
        var safeColumns = new List<string>();
        if (detailColumns is { Count: > 0 })
        {
            var allowed = (await GetDetailColumnsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            safeColumns = detailColumns.Where(c => allowed.Contains(c)).Distinct().ToList();
        }

        string sql;
        if (safeColumns.Count == 0)
        {
            sql = baseSql;
        }
        else
        {
            // Join item_details onto the (already filtered + capped) base result for the picked columns.
            var companyId = EscapeString(query.CompanyId ?? "");
            var detailSelect = string.Join(", ", safeColumns.Select(c => $"id.{Q(c)} AS {Q(c)}"));
            sql = $@"
                SELECT b.*, {detailSelect}
                FROM ( {baseSql} ) AS b
                LEFT JOIN daily_inventory.item_details AS id
                    ON id.company_id = '{companyId}'
                    AND id.item_no = b.item_no
                    AND id.variant_code = b.variant_code
                ORDER BY b.item_no, b.variant_code, b.location_code";
        }

        _logger.LogInformation("Daily inventory export query (company {CompanyId}, cap {Cap}):\n{Sql}",
            query.CompanyId, ExportRowCap, sql);

        using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var writer = new StreamWriter(output, new UTF8Encoding(false), bufferSize: 64 * 1024, leaveOpen: true);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var fieldCount = reader.FieldCount;

        // Header row (uses the ClickHouse column names / aliases).
        for (var i = 0; i < fieldCount; i++)
        {
            if (i > 0) await writer.WriteAsync(',');
            await writer.WriteAsync(CsvEscape(reader.GetName(i)));
        }
        await writer.WriteAsync("\r\n");

        var rowsWritten = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            for (var i = 0; i < fieldCount; i++)
            {
                if (i > 0) await writer.WriteAsync(',');
                await writer.WriteAsync(CsvEscape(FormatCsvValue(reader.GetValue(i))));
            }
            await writer.WriteAsync("\r\n");

            if (++rowsWritten % 5000 == 0)
                await writer.FlushAsync();
        }

        await writer.FlushAsync();
    }

    private static string FormatCsvValue(object? value) => value switch
    {
        null => "",
        DBNull => "",
        string s => s,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private static string CsvEscape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string EscapeString(string value)
        => value.Replace("'", "\\'").Replace("\\", "\\\\");

    /// <summary>Backtick-quotes a ClickHouse identifier (column names are validated against the schema before use).</summary>
    private static string Q(string identifier)
        => "`" + identifier.Replace("`", "``") + "`";

    private static string? ToStringValue(object? value) => value switch
    {
        null => null,
        DBNull => null,
        string s => s,
        _ => value.ToString()
    };

    public async Task<List<DailyInventoryLocation>> GetLocationsAsync(string companyId, CancellationToken cancellationToken = default)
    {
        var escapedCompanyId = EscapeString(companyId);
        var sql = $@"
            SELECT code, name
            FROM daily_inventory.location
            WHERE company_id = '{escapedCompanyId}'
              AND code IS NOT NULL
            ORDER BY code";

        using var connection = new ClickHouseConnection(_settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var locations = new List<DailyInventoryLocation>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            locations.Add(new DailyInventoryLocation
            {
                Code = reader.GetValue(0) as string,
                Name = reader.GetValue(1) as string,
            });
        }

        return locations;
    }

    private static double ToDouble(object? value) => value switch
    {
        double d  => d,
        float f   => f,
        decimal m => (double)m,
        long l    => l,
        int i     => i,
        null      => 0,
        DBNull    => 0,
        _         => Convert.ToDouble(value)
    };
}

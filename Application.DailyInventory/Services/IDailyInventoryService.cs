using Application.Shared.Models.DailyInventory;

namespace Application.DailyInventory.Services;

public interface IDailyInventoryService
{
    Task<List<DailyInventoryRow>> GetDailyInventoryAsync(DailyInventoryQuery query, CancellationToken cancellationToken = default);
    Task<List<DailyInventoryLocation>> GetLocationsAsync(string companyId, CancellationToken cancellationToken = default);

    /// <summary>Available item_details columns (discovered via DESCRIBE), excluding the key columns.</summary>
    Task<List<string>> GetDetailColumnsAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches the requested item_details columns for the given items, keyed by item_no + variant_code.</summary>
    Task<List<ItemDetailsRow>> GetItemDetailsAsync(string companyId, List<string> columns, List<ItemKey> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the full filtered result set (ignoring pagination, capped at 1,000,000 rows) as CSV to
    /// <paramref name="output"/>, optionally including the given item_details columns.
    /// </summary>
    Task ExportCsvAsync(DailyInventoryQuery query, List<string> detailColumns, Stream output, CancellationToken cancellationToken = default);
}

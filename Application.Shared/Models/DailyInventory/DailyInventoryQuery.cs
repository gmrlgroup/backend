namespace Application.Shared.Models.DailyInventory;

public class DailyInventoryQuery
{
    public string? CompanyId { get; set; }

    // Rolling end date — weeks are calculated backwards from this date
    public DateTime EndDate { get; set; } = DateTime.Today;

    // Optional text filters (case-insensitive substring match) on item_location columns.
    public string? ItemNo { get; set; }
    public string? VariantCode { get; set; }
    /// <summary>Multi-select: filter to rows whose location_code is in this list. Null/empty = no filter.</summary>
    public List<string>? LocationCodes { get; set; }

    /// <summary>When true, only return rows with positive stock-on-hand.</summary>
    public bool HasStock { get; set; }
    /// <summary>When true, only return rows with sales (non-zero) in any of the 6 rolling weeks.</summary>
    public bool HasSales { get; set; }

    // Pagination
    public int Offset { get; set; } = 0;
    public int Limit { get; set; } = 1000;
}

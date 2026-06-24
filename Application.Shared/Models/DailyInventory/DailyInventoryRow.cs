namespace Application.Shared.Models.DailyInventory;

public class DailyInventoryRow
{
    public string? ItemNo { get; set; }
    public string? VariantCode { get; set; }
    public string? LocationCode { get; set; }

    // Week 1 = most recent week (endDate-7d to endDate)
    public double Week1Qty { get; set; }
    // Week 2 = endDate-14d to endDate-7d
    public double Week2Qty { get; set; }
    // Week 3 = endDate-21d to endDate-14d
    public double Week3Qty { get; set; }
    // Week 4 = endDate-28d to endDate-21d
    public double Week4Qty { get; set; }
    // Week 5 = endDate-35d to endDate-28d
    public double Week5Qty { get; set; }
    // Week 6 = endDate-42d to endDate-35d (oldest week)
    public double Week6Qty { get; set; }

    // Sum of all transaction quantities up to endDate
    public double StockOnHand { get; set; }

    /// <summary>
    /// Optional item_details attributes, fetched from a separate query and joined in only for the
    /// columns the user selects in the sidebar. Key = column name, value = stringified cell value.
    /// </summary>
    public Dictionary<string, string?> Details { get; set; } = new();
}

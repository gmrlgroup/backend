namespace Application.Shared.Models.Dashboards.Oos;

/// <summary>
/// One affected item-at-location row, reduced to only the fields the dashboard aggregates
/// (KPIs, breakdown chart, store + vendor lists). Per-product detail (SKU, description, price,
/// velocity) is intentionally NOT returned — item-level detail lives in the linked dataset.
/// State / DaysOut / Lost are computed server-side.
/// </summary>
public class OosItem
{
    public string Category { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Warehouse { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;

    /// <summary>"out" | "low" | "restocking".</summary>
    public string State { get; set; } = "out";

    /// <summary>Days unavailable (0 for low-stock rows).</summary>
    public int DaysOut { get; set; }

    /// <summary>Modelled lost sales for this item-location.</summary>
    public double Lost { get; set; }
}

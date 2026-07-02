namespace Application.Shared.Models.Dashboards.Oos;

/// <summary>
/// Full payload for the Out-of-Stock dashboard. The page filters/sorts/aggregates this set
/// client-side (KPIs, charts, store + vendor lists, table), mirroring the original static report.
/// </summary>
public class OosDashboardResponse
{
    /// <summary>Reporting "as of" date the stock positions were computed at.</summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>Distinct sellable item+variant count for the company — denominator for the OOS rate.</summary>
    public int TotalCatalog { get; set; }

    /// <summary>Number of (non-transit) store locations in the network.</summary>
    public int TotalStores { get; set; }

    /// <summary>Affected items (out / low / restocking).</summary>
    public List<OosItem> Items { get; set; } = new();

    /// <summary>Vendor order fill-rate (%) keyed by vendor_no, over the trailing window.</summary>
    public Dictionary<string, double> VendorFillRates { get; set; } = new();

    /// <summary>Monthly OOS-rate trend: current year (YTD) vs prior year.</summary>
    public List<OosTrendPoint> Trend { get; set; } = new();

    /// <summary>
    /// Relative URL of the ingested dataset that holds the full item-level detail. When set, the
    /// dashboard shows a "view details" link that navigates here. Configured via
    /// <c>Dashboards:Oos:DetailsUrl</c>.
    /// </summary>
    public string? DetailsUrl { get; set; }
}

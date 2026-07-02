namespace Application.Shared.Models.Dashboards.Oos;

/// <summary>
/// One month of the OOS-rate trend: the share of the Live catalog out of stock that month,
/// for the current year (year-to-date) and the prior year. Future months have a null
/// <see cref="ThisYear"/> so the chart line stops at the current month.
/// </summary>
public class OosTrendPoint
{
    public int Month { get; set; }            // 1..12
    public string Label { get; set; } = string.Empty; // "Jan".."Dec"
    public double? ThisYear { get; set; }     // YTD OOS rate (%) — null for future months
    public double? PriorYear { get; set; }    // prior-year OOS rate (%)
}

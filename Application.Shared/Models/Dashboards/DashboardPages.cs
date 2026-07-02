using System.Collections.Generic;

namespace Application.Shared.Models.Dashboards;

/// <summary>Dashboard pages that can be connected to an ingested dataset table.</summary>
public static class DashboardPages
{
    public const string Oos = "/dashboards/oos";

    public static readonly IReadOnlyList<DashboardPageInfo> All = new[]
    {
        new DashboardPageInfo("Out-of-Stock Report", Oos)
    };
}

/// <summary>A dashboard the user can link a table to.</summary>
public record DashboardPageInfo(string Label, string PageUrl);

/// <summary>Request to connect a dashboard page to a dataset table.</summary>
public class DashboardLinkRequest
{
    public string PageUrl { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

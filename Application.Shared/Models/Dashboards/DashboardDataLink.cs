using System;

namespace Application.Shared.Models.Dashboards;

/// <summary>
/// Links a dashboard page (by its route, e.g. <c>/dashboards/oos</c>) to an ingested dataset table,
/// so the dashboard's "view details" action can deep-link into that table. One link per
/// (company, page) — connecting a new table to a page replaces the previous link.
/// </summary>
public class DashboardDataLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>The dashboard page route this link is for (e.g. <c>/dashboards/oos</c>).</summary>
    public string PageUrl { get; set; } = string.Empty;

    public string DatasetId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

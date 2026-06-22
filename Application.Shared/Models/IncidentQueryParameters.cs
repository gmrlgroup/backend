using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>Filter/sort/paging options for the incidents list. Bound from the query string.</summary>
public class IncidentQueryParameters
{
    private const int MaxPageSize = 200;
    private int _pageSize = 10;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : Math.Min(value, MaxPageSize);
    }

    /// <summary>Free-text match against title, description, entity name.</summary>
    public string? Search { get; set; }

    public IncidentSeverity? Severity { get; set; }

    public IncidentStatus? Status { get; set; }

    /// <summary>Filter by the affected entity's type (Server, Database, Report, ...).</summary>
    public AssetType? EntityType { get; set; }

    /// <summary>When true, excludes resolved incidents.</summary>
    public bool ActiveOnly { get; set; }

    /// <summary>One of: title, entity, severity, status, started, resolved. Defaults to started.</summary>
    public string SortBy { get; set; } = "started";

    /// <summary>asc or desc.</summary>
    public string SortDir { get; set; } = "desc";
}

using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>Filter/sort/paging options for the entities list. Bound from the query string.</summary>
public class EntityQueryParameters
{
    private const int MaxPageSize = 200;
    private int _pageSize = 10;

    public int Page { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : Math.Min(value, MaxPageSize);
    }

    /// <summary>Free-text match against name, description, owner, location.</summary>
    public string? Search { get; set; }

    public AssetType? Type { get; set; }

    /// <summary>Active/inactive filter; null = both.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Critical-only filter; null = both.</summary>
    public bool? IsCritical { get; set; }

    /// <summary>Latest reported status; null = any.</summary>
    public AssetStatus? CurrentStatus { get; set; }

    public string? Group { get; set; }

    /// <summary>One of: name, type, group, owner, status, active. Defaults to name.</summary>
    public string SortBy { get; set; } = "name";

    /// <summary>asc or desc.</summary>
    public string SortDir { get; set; } = "asc";
}

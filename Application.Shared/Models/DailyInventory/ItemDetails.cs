using System.Collections.Generic;

namespace Application.Shared.Models.DailyInventory;

/// <summary>Identifies one item_location row to look up item_details for.</summary>
public class ItemKey
{
    public string? ItemNo { get; set; }
    public string? VariantCode { get; set; }
}

/// <summary>Request body for fetching selected item_details columns for a set of items.</summary>
public class ItemDetailsRequest
{
    /// <summary>Column names from item_details to return (validated server-side against the real schema).</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>The items (by item_no + variant_code) to fetch details for — typically the loaded page.</summary>
    public List<ItemKey> Items { get; set; } = new();
}

/// <summary>The selected item_details values for a single item, keyed by column name.</summary>
public class ItemDetailsRow
{
    public string? ItemNo { get; set; }
    public string? VariantCode { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new();
}

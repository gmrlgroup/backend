using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Models.Data;

public class TableDataQuery
{
    /// <summary>Reserved key under which each row carries its DuckDB <c>rowid</c> when
    /// <see cref="IncludeRowId"/> is requested. Not a real column — used to target row edits/deletes.</summary>
    public const string RowIdKey = "__rowid";

    public string DatasetId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<string>? SelectedColumns { get; set; }
    public List<SortColumn>? SortColumns { get; set; }
    public List<FilterCondition>? Filters { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    // When true, each returned row carries its DuckDB rowid under RowIdKey (excluded from Columns) so the
    // data viewer can update/delete the exact row. Only meaningful for local DuckDB base tables.
    public bool IncludeRowId { get; set; }
}

/// <summary>A single-row edit payload from the data viewer. <see cref="RowId"/> identifies the target
/// row for updates/deletes (the DuckDB rowid); it is null for inserts. Values are the raw string form
/// of each column, cast to the column type server-side.</summary>
public class RowEditModel
{
    public long? RowId { get; set; }
    public Dictionary<string, string?> Values { get; set; } = new();
}

/// <summary>Outcome of an insert/update/delete on a table row. Errors are returned via
/// <see cref="Error"/> rather than thrown.</summary>
public class RowMutationResult
{
    public bool Success { get; set; }
    public int RowsAffected { get; set; }
    public string? Error { get; set; }
}

/// <summary>A batch of row changes from the spreadsheet editor, applied atomically (all-or-nothing).
/// Updates carry the target rowid plus only the columns that changed; inserts carry the new column
/// values; deletes are the target rowids.</summary>
public class BulkRowEditRequest
{
    public List<RowEditModel> Updates { get; set; } = new();
    public List<RowEditModel> Inserts { get; set; } = new();
    public List<long> Deletes { get; set; } = new();
}

/// <summary>Outcome of a <see cref="BulkRowEditRequest"/>. On failure nothing is committed and
/// <see cref="Error"/> explains why.</summary>
public class BulkRowEditResult
{
    public bool Success { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public string? Error { get; set; }
}

public class SortColumn
{
    public string ColumnName { get; set; } = string.Empty;
    public bool IsDescending { get; set; } = false;
}

public class TableDataResult
{
    public List<Dictionary<string, object>> Data { get; set; } = new();
    public List<Column> Columns { get; set; } = new();
    public int TotalRows { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ColumnSettings
{
    public string ColumnName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
    //public int Width { get; set; } = 120;
}

public class UserColumnPreferences
{
    public string UserId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<ColumnSettings> ColumnSettings { get; set; } = new();
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

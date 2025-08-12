using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Models.Data;

public class TableDataQuery
{
    public string DatasetId { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<string>? SelectedColumns { get; set; }
    public List<SortColumn>? SortColumns { get; set; }
    public List<FilterCondition>? Filters { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
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

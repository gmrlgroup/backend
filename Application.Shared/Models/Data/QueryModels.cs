using System;
using System.Collections.Generic;

namespace Application.Shared.Models.Data;

/// <summary>Request to run an ad-hoc SQL statement against a dataset.</summary>
public class SqlQueryRequest
{
    public string Sql { get; set; } = string.Empty;
    // Optional cap on returned rows; the service clamps this to a hard maximum.
    public int? MaxRows { get; set; }
    // For External datasets: when true, query the dataset's local DuckDB snapshots instead of the live
    // source connection. Ignored for Local datasets (they are always DuckDB).
    public bool SnapshotMode { get; set; }
}

/// <summary>Result of running ad-hoc SQL. SQL errors are returned via <see cref="Error"/> (not thrown).</summary>
public class SqlQueryResult
{
    public List<Column> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowsReturned { get; set; }
    // True when more rows existed than the row cap returned.
    public bool Truncated { get; set; }
    public long ElapsedMs { get; set; }
    // For write statements (INSERT/UPDATE/DELETE/DDL) that don't return a result set.
    public int RowsAffected { get; set; }
    public bool IsSelect { get; set; }
    public string? Error { get; set; }
}

/// <summary>Write-back: materialize a query as a new table or view in the dataset.</summary>
public class SaveResultRequest
{
    public string Sql { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public bool AsView { get; set; }
    // For External datasets in source mode: snapshot the live query result into a local DuckDB table.
    // When true (or for Local datasets) the SQL targets DuckDB and AsView applies as usual.
    public bool SnapshotMode { get; set; }
}

public class SavedQueryDto
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    // Whether the requesting user may edit/delete this query (creator or admin).
    public bool CanEdit { get; set; }
}

public class SaveSavedQueryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public bool IsShared { get; set; }
}

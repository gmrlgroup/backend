namespace Application.Shared.Models.Data;

/// <summary>Aggregate annotations for a dataset shown on the datasets list page:
/// how many tables it holds, its on-disk DuckDB file size, an estimated total row count,
/// and a coarse status. Computed on demand (not persisted).</summary>
public class DatasetStats
{
    public string DatasetId { get; set; } = string.Empty;

    /// <summary>Whether the dataset's DuckDB file exists on disk.</summary>
    public bool DatabaseExists { get; set; }

    public int TableCount { get; set; }

    /// <summary>Size of the dataset's DuckDB file on disk, in bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Sum of the estimated row counts across all tables.</summary>
    public long TotalRows { get; set; }

    /// <summary>Coarse lifecycle label: "External", "No database", "Empty", or "In use".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Number of users the dataset is shared with (dataset-level shares); 0 = private.</summary>
    public int SharedWith { get; set; }
}

/// <summary>Per-table stats for the tables list page. Storage/schema fields come from the DuckDB
/// catalog; ingestion and owner fields are enriched from the app's ingestion sources + dataset.
/// Computed on demand, never persisted.</summary>
public class TableStats
{
    public string TableName { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public int ColumnCount { get; set; }
    public long SizeBytes { get; set; }

    // --- Schema summary (DuckDB catalog) ---
    public bool HasPrimaryKey { get; set; }
    /// <summary>Column-type mix, e.g. "5 text · 3 num · 1 date". Empty when the table has no columns.</summary>
    public string TypeSummary { get; set; } = string.Empty;

    // --- Ingestion (set only when the table is the target of a scheduled ingestion source) ---
    public bool IsIngested { get; set; }
    public bool IngestionEnabled { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastRunStatus { get; set; }
    public int? LastRunRows { get; set; }

    // --- Owner / created ---
    /// <summary>Ingestion source creator for ingested tables, else the dataset creator.</summary>
    public string? Owner { get; set; }
    /// <summary>Creation time for ingested tables (from the ingestion source); null when unknown.</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Number of users who can access this table (full-dataset + table-scoped shares); 0 = private.</summary>
    public int SharedWith { get; set; }
}

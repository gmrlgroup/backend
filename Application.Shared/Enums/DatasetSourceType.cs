namespace Application.Shared.Enums;

/// <summary>
/// Where a dataset's tables live. <see cref="Local"/> datasets own a DuckDB file the user fills
/// via imports/ingestion. <see cref="External"/> datasets are backed by a connected Database entity
/// (MonitoredAsset) and queried live; the dataset's DuckDB file is then used only for saved snapshots.
/// </summary>
public enum DatasetSourceType
{
    Local = 0,
    External = 1
}

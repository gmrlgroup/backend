namespace Application.Shared.Models;

/// <summary>Safe, browser-facing view of a <see cref="PowerBiConnection"/> — never includes the secret.</summary>
public class PowerBiConnectionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    /// <summary>True when a secret is stored, so the UI can show a masked placeholder.</summary>
    public bool HasSecret { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
}

/// <summary>Create/update payload. <see cref="ClientSecret"/> is plaintext and optional on update (blank keeps the existing secret).</summary>
public class PowerBiConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Browser-facing view of a <see cref="PowerBiDatasetLink"/>.</summary>
public class PowerBiDatasetLinkDto
{
    public string Id { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PowerBiConnectionId { get; set; } = string.Empty;
    public string? ConnectionName { get; set; }
    public string WorkspaceId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
}

/// <summary>Create/update payload for an entity's Power BI dataset link.</summary>
public class PowerBiDatasetLinkRequest
{
    public string PowerBiConnectionId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
}

/// <summary>One entry from a dataset's Power BI refresh history.</summary>
public class PowerBiRefreshDto
{
    public string? RequestId { get; set; }
    /// <summary>e.g. "ViaApi", "Scheduled", "OnDemand".</summary>
    public string? RefreshType { get; set; }
    /// <summary>"Completed", "Failed", "Disabled", or "Unknown" (in progress).</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    /// <summary>Human-readable error extracted from the failure payload, when the refresh failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>A dataset's scheduled-refresh configuration, plus the computed next run.</summary>
public class PowerBiRefreshScheduleDto
{
    public bool Enabled { get; set; }
    /// <summary>Day names the refresh runs on (e.g. "Monday").</summary>
    public List<string> Days { get; set; } = new();
    /// <summary>Times of day the refresh runs, "HH:mm".</summary>
    public List<string> Times { get; set; } = new();
    /// <summary>Windows time-zone id the schedule is expressed in (e.g. "UTC").</summary>
    public string? LocalTimeZoneId { get; set; }
    public string? NotifyOption { get; set; }
    /// <summary>Next scheduled refresh instant in UTC, computed from days/times/timezone; null if none upcoming or disabled.</summary>
    public DateTime? NextRefresh { get; set; }
}

/// <summary>What a dataset draws from: its data sources (databases) and tables, with any existing-entity matches.</summary>
public class PowerBiDiscoveryDto
{
    public List<PowerBiDataSourceDto> DataSources { get; set; } = new();
    public List<PowerBiTableInfoDto> Tables { get; set; } = new();
    /// <summary>Set when table discovery couldn't run (e.g. the "Dataset Execute Queries" tenant setting is off). Databases still resolve.</summary>
    public string? TablesError { get; set; }
}

/// <summary>A data source behind the dataset (typically a SQL server + database).</summary>
public class PowerBiDataSourceDto
{
    public string? DatasourceType { get; set; }
    public string? Server { get; set; }
    public string? Database { get; set; }
    /// <summary>Id of an existing Database entity that matches this database name (same company); null if none.</summary>
    public string? ExistingEntityId { get; set; }
    public string? ExistingEntityName { get; set; }
}

/// <summary>A table in the dataset's model, with any existing Table-entity match.</summary>
public class PowerBiTableInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string? ExistingEntityId { get; set; }
}

/// <summary>User's chosen databases + tables to materialize as entities and wire into the dependency graph.</summary>
public class PowerBiLineageCommitRequest
{
    public List<PowerBiDbSelection> Databases { get; set; } = new();
    public List<string> Tables { get; set; } = new();
}

public class PowerBiDbSelection
{
    public string Database { get; set; } = string.Empty;
    public string? Server { get; set; }
}

/// <summary>Summary of what a lineage commit created or updated.</summary>
public class PowerBiLineageCommitResult
{
    public int DatabasesAdded { get; set; }
    public int DatabasesUpdated { get; set; }
    public int TablesAdded { get; set; }
    public int TablesUpdated { get; set; }
    public int DependenciesCreated { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>Result of a Power BI action (trigger refresh, save link, etc.).</summary>
public class PowerBiActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static PowerBiActionResult Ok(string message) => new() { Success = true, Message = message };
    public static PowerBiActionResult Fail(string message) => new() { Success = false, Message = message };
}

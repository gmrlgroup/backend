using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// Read-only payload for the public status board: monitored assets grouped by type,
/// each with a per-day status timeline over a rolling window.
/// </summary>
public class StatusOverviewDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int Days { get; set; }
    public List<StatusOverviewTypeGroup> Groups { get; set; } = new();
}

/// <summary>One card on the board: all entities of a single <see cref="AssetType"/>.</summary>
public class StatusOverviewTypeGroup
{
    public AssetType EntityType { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public int EntityCount { get; set; }

    /// <summary>Worst current status across the group's entities — drives the card header dot.</summary>
    public AssetStatus WorstCurrentStatus { get; set; }

    public List<StatusOverviewEntity> Entities { get; set; } = new();
}

/// <summary>One entity row: its current status plus a day-by-day timeline.</summary>
public class StatusOverviewEntity
{
    public string EntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetStatus CurrentStatus { get; set; }

    /// <summary>Percentage of days-with-data whose last status was Online.</summary>
    public double UptimePercent { get; set; }

    /// <summary>Oldest → newest; length equals <see cref="StatusOverviewDto.Days"/>.</summary>
    public List<StatusOverviewDay> Days { get; set; } = new();
}

/// <summary>A single day in an entity's timeline. <see cref="Status"/> is null when no data was recorded.</summary>
public class StatusOverviewDay
{
    public DateTime Date { get; set; }
    public AssetStatus? Status { get; set; }
}

/// <summary>A single status-history event, returned when a day cell is expanded.</summary>
public class StatusDayEventDto
{
    public DateTime CheckedAt { get; set; }
    public AssetStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public double? ResponseTime { get; set; }
}

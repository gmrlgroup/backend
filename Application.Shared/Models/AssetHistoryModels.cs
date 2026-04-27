using System.ComponentModel.DataAnnotations;
using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// Request model for creating asset status history records.
/// </summary>
public class CreateAssetStatusHistoryRequest
{
    [Required(ErrorMessage = "EntityId is required")]
    public string EntityId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Status is required")]
    public AssetStatus Status { get; set; }

    [MaxLength(2000)]
    public string? StatusMessage { get; set; }

    [Range(0, double.MaxValue)]
    public double? ResponseTime { get; set; }

    [Range(0, 100)]
    public double? UptimePercentage { get; set; }

    public DateTime? CheckedAt { get; set; }
}

/// <summary>
/// Request model for updating asset status history records.
/// </summary>
public class UpdateAssetStatusHistoryRequest
{
    [Required(ErrorMessage = "Status is required")]
    public AssetStatus Status { get; set; }

    [MaxLength(2000)]
    public string? StatusMessage { get; set; }

    [Range(0, double.MaxValue)]
    public double? ResponseTime { get; set; }

    [Range(0, 100)]
    public double? UptimePercentage { get; set; }

    [Required(ErrorMessage = "CheckedAt is required")]
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// Response model for asset status history records.
/// </summary>
public class AssetStatusHistoryResponse
{
    public int Id { get; set; }
    public string EntityId { get; set; } = string.Empty;
    public AssetStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public double? ResponseTime { get; set; }
    public double? UptimePercentage { get; set; }
    public DateTime? CheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request model for querying asset status history with filters.
/// </summary>
public class AssetStatusHistoryQueryRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public AssetStatus? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 1000)]
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Paginated response for asset status history queries.
/// </summary>
public class AssetStatusHistoryPagedResponse
{
    public List<AssetStatusHistoryResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Summary statistics for an asset's status history.
/// </summary>
public class AssetStatusSummaryResponse
{
    public string EntityId { get; set; } = string.Empty;
    public Dictionary<AssetStatus, int> StatusCounts { get; set; } = new();
    public double? AverageResponseTime { get; set; }
    public double? AverageUptime { get; set; }
    public int TotalChecks { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

using System.ComponentModel.DataAnnotations;
using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// Request model for creating an incident.
/// </summary>
public class CreateIncidentRequest
{
    [Required(ErrorMessage = "EntityId is required")]
    public string EntityId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [MaxLength(4000, ErrorMessage = "Description cannot exceed 4000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Severity is required")]
    public IncidentSeverity Severity { get; set; }

    [MaxLength(200)]
    public string? ReportedBy { get; set; }

    [MaxLength(200)]
    public string? AssignedTo { get; set; }

    [MaxLength(1000)]
    public string? ImpactDescription { get; set; }

    [MaxLength(100)]
    public string? ExternalIncidentId { get; set; }

    [MaxLength(4000)]
    public string? Metadata { get; set; }

    public DateTime? StartedAt { get; set; }
}

/// <summary>
/// Response model for a created incident.
/// </summary>
public class CreateIncidentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IncidentStatus Status { get; set; }
    public IncidentSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public string? ExternalIncidentId { get; set; }
}

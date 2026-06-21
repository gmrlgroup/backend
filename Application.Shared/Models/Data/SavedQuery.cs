using System;
using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Models.Data;

/// <summary>
/// A saved SQL query against a dataset's DuckDB tables. Private to its creator unless
/// <see cref="IsShared"/> is set, in which case every user with access to the dataset
/// (in the same company) can see and run it.
/// </summary>
public class SavedQuery
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CompanyId { get; set; } = string.Empty;

    public string DatasetId { get; set; } = string.Empty;

    [Required(ErrorMessage = "A name is required")]
    [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [Required]
    public string QueryText { get; set; } = string.Empty;

    public bool IsShared { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

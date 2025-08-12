using Application.Shared.Models.User;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models;

[PrimaryKey(nameof(DatasetId), nameof(UserId))]
public class DatasetUser
{
    [Required]
    public string DatasetId { get; set; } = string.Empty;
    public Dataset? Dataset { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required]
    public DatasetUserType Type { get; set; } = DatasetUserType.Viewer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
}

public enum DatasetUserType
{
    Admin = 0,
    Editor = 1,
    Viewer = 2
}

public class ShareDatasetRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string DatasetId { get; set; } = string.Empty;

    [Required]
    public DatasetUserType UserType { get; set; } = DatasetUserType.Viewer;
}

public class DatasetUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DatasetUserType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

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
    // public ApplicationUser? User { get; set; }

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

/// <summary>
/// Scopes a dataset share to specific tables. A (DatasetId, UserId) with NO rows here has access to
/// ALL tables in the dataset; one or more rows restrict the user to exactly those tables.
/// </summary>
[PrimaryKey(nameof(DatasetId), nameof(UserId), nameof(TableName))]
public class DatasetUserTable
{
    [Required]
    public string DatasetId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string TableName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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

    /// <summary>
    /// Tables this user may access. Null or empty = all tables (full dataset access). When set, the
    /// user's table scope is REPLACED with exactly these tables.
    /// </summary>
    public List<string>? Tables { get; set; }
}

/// <summary>Additive single-table grant (used by the "share this table" action). Does not downgrade
/// a user who already has full dataset access.</summary>
public class GrantTableShareRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string DatasetId { get; set; } = string.Empty;

    [Required]
    public string TableName { get; set; } = string.Empty;

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

    /// <summary>Tables this user is scoped to. Empty = all tables (full access).</summary>
    public List<string> Tables { get; set; } = new();
}

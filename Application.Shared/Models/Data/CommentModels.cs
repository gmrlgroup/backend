using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models.Data;

public class DataTableComment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string DatasetId { get; set; } = string.Empty;

    [Required]
    public string TableName { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public List<string> MentionedUserIds { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
}

public class UserMention
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

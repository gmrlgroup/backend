using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Shared.Models.Data;

public class Table
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Schema name is required")]
    [StringLength(100, ErrorMessage = "Schema name cannot exceed 100 characters")]
    [MinLength(1, ErrorMessage = "Schema name must be at least 1 character long")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Schema name must start with a letter and contain only letters, numbers, and underscores")]
    public string SchemaName { get; set; } = "main";

    [Required(ErrorMessage = "Table name is required")]
    [StringLength(100, ErrorMessage = "Table name cannot exceed 100 characters")]
    [MinLength(1, ErrorMessage = "Table name must be at least 1 character long")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Table name must start with a letter and contain only letters, numbers, and underscores")]
    public string TableName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company ID is required")]
    public string CompanyId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dataset ID is required")]
    public string DatasetId { get; set; } = string.Empty;

    public List<Column>? Columns { get; set; } = new List<Column>();

    public string? CreatedBy { get; set; } = string.Empty;
    public DateTime? CreatedOn { get; set; } = DateTime.UtcNow;
}

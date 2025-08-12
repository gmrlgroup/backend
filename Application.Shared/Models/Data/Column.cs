using System.ComponentModel.DataAnnotations;

namespace Application.Shared.Models.Data;

public class Column
{
    [Required(ErrorMessage = "Column name is required")]
    [StringLength(100, ErrorMessage = "Column name cannot exceed 100 characters")]
    [MinLength(1, ErrorMessage = "Column name must be at least 1 character long")]
    [RegularExpression(@"^[a-zA-Z][a-zA-Z0-9_]*$", ErrorMessage = "Column name must start with a letter and contain only letters, numbers, and underscores")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Data type is required")]
    public string DataType { get; set; } = "VARCHAR";

    public bool IsNullable { get; set; } = true;

    public string? DefaultValue { get; set; }

    public bool? IsPrimaryKey { get; set; }

    // Helper property to get common data types for dropdown
    public static List<string> CommonDataTypes => new List<string>
    {
        "VARCHAR",
        "TEXT",
        "INTEGER",
        "BIGINT",
        "DECIMAL",
        "DOUBLE",
        "BOOLEAN",
        "DATE",
        "TIMESTAMP",
        "TIME",
        "UUID",
        "JSON",
        "BLOB"
    };
}

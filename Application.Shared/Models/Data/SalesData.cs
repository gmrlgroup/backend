using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models.Data;

public class SalesData : BaseModel
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Scheme is required")]
    [StringLength(100, ErrorMessage = "Scheme cannot exceed 100 characters")]
    public string? Scheme { get; set; }

    [Required(ErrorMessage = "Store code is required")]
    [StringLength(50, ErrorMessage = "Store code cannot exceed 50 characters")]
    public string? StoreCode { get; set; }

    [StringLength(500, ErrorMessage = "Division code cannot exceed 50 characters")]
    public string? DivisionName { get; set; }


    [StringLength(500, ErrorMessage = "Category code cannot exceed 50 characters")]
    public string? CategoryName { get; set; }


    [Required(ErrorMessage = "DateTime is required")]
    public int Hour { get; set; }

    [Required(ErrorMessage = "Net amount is required")]
    [Column(TypeName = "decimal(18,2)")]
    [JsonPropertyName("NetAmountAcy")]
    public decimal NetAmountAcy { get; set; }
    
    [Required(ErrorMessage = "Total transactions is required")]
    public int TotalTransactions { get; set; }

    public decimal? TotalStoreTransactions { get; set; } // the value is decimal because it's averaged in the query

    // Additional properties for real-time tracking
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public string? Source { get; set; } // Source system identifier
    public bool IsProcessed { get; set; } = false;
}

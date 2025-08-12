using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

public class Dataset
{

    public string CompanyId { get; set; }
    public Company? Company { get; set; }

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Dataset name is required")]
    [StringLength(100, ErrorMessage = "Dataset name cannot exceed 100 characters")]
    [MaxLength(100)]
    [MinLength(3, ErrorMessage = "Dataset name must be at least 3 characters long")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Dataset name can only contain letters, numbers, and spaces")]
    [Display(Name = "Dataset Name")]
    public string? Name { get; set; }


    [Required(ErrorMessage = "Dataset type is required")]
    [StringLength(500, ErrorMessage = "Dataset type cannot exceed 50 characters")]
    [MaxLength(500)]
    [MinLength(3, ErrorMessage = "Dataset type must be at least 3 characters long")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Dataset type can only contain letters, numbers, and spaces")]
    public string? Description { get; set; }

    // User who created the dataset
    public string? CreatedBy { get; set; }
    
    // Creation and modification timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

}

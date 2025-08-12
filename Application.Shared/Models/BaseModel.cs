using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

public class BaseModel
{
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime? CreatedOn { get; set; } = DateTime.Now;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime? ModifiedOn { get; set; } = DateTime.Now;


    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    //[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    //public bool IsDeleted { get; set; } = false;


    [NotMapped]
    public bool? IsSelected { get; set; }
}

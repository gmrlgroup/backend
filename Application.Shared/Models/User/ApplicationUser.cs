using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models.User;



// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser
{
    //public string Email { get; set; }
    public DateTime? CreatedOn { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? CreatedBy { get; set; }

    public string? ModifiedBy { get; set; }

    public bool? IsDeleted { get; set; }


    //public string UserName { get; set;  }

    [NotMapped]
    public string? Password1 { get; set; }
    
    [NotMapped]    
    public string? Password2 { get; set; }

    [NotMapped]
    public bool IsSelected { get; set; }
}

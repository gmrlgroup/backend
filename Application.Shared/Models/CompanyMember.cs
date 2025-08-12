using Application.Shared.Models.User;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

[PrimaryKey(nameof(CompanyId), nameof(ApplicationUserId))]
public class CompanyMember
{
    [MaxLength(10)]
    public string? CompanyId { get; set; }
    public Company? Company { get; set; }

    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }
}

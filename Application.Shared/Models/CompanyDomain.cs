using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

[PrimaryKey(nameof(CompanyId), nameof(Domain))]
public class CompanyDomain
{
    public string CompanyId { get; set;  }
    public Company Company { get; set; }
    public string Domain { get; set; }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models.Data;

public class Database
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string HostIp { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = string.Empty;
    public string DefaultLoginUser { get; set; } = string.Empty;
    public string DefaultLoginPassword { get; set; } = string.Empty;

}

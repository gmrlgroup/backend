using Application.Shared.Enums;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

public class DataState  
{
    public int Page { get; set; } = 0;

    public int PageSize { get; set; }
    public string? SortLabel { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;

}

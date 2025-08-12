using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Shared.Models;

public class FilterCondition
{
    public string ColumnName { get; set; }
    public string Operator { get; set; }
    public string Value { get; set; }
    public string LogicalOperator { get; set; } = "AND"; // Default logical operator
}


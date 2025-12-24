using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Scheduler.Models;

public class SalesGroupedByStoreHour
{
    public string Scheme { get; set; }
    public string StoreCode { get; set; }

    public int Hour { get; set; }

    public string? DivisionName { get; set; }
    public string? CategoryName { get; set; }

    public decimal NetAmountAcy { get; set; }
    public decimal TotalStoreTransactions {get; set; }
    public int TotalTransactions { get; set; }

    public string SalesOrderNumber { get; set; }

}

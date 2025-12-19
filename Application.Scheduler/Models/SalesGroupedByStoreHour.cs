using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Scheduler.Models;

public class SalesGroupedByStoreHour
{
    public string Scheme { get; set; }
    public string StoreCode { get; set; }

    public int Hour { get; set; }

    public decimal NetAmountAcy { get; set; }
    public int TotalTransactions { get; set; }

    public string SalesOrderNumber { get; set; }

}

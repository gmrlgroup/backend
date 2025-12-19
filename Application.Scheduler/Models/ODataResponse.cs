using System.Text.Json.Serialization;

namespace Application.Scheduler.Models;


public class ODataResponse<T>
{
    public List<T> value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string NextLink { get; set; }
}



public class SalesLineFO
{
    public string ShippingWarehouseId { get; set; }
    public decimal LineAmount { get; set; }

    [JsonPropertyName("SalesOrderHeader")]
    public SalesHeaderFO SalesHeader { get; set; }
}


public class SalesHeaderFO
{
    public string CurrencyCode { get; set; }


    [JsonPropertyName("SalesOrderNumber")]
    public string SalesOrderNumber { get; set; }
}
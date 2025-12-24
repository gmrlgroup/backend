using Application.Scheduler.Models;
using Application.Shared.Models.Data;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.Json;

namespace Application.Scheduler.Repositories;

public interface ISalesRepository
{
    Task<List<SalesGroupedByStoreHour>> GetSalesGroupedByStoreHour(Database database);

    Task<List<SalesGroupedByStoreHour>> GetNokNokSalesGroupedByStoreHour(Database database);

    Task<List<SalesGroupedByStoreHour>> GetNokNokSalesGroupedByStoreHourFO();
}


public class SalesRepository : ISalesRepository
{
    //private readonly string _connectionString;
    private readonly HttpClient _httpClient;
    private readonly string _salesUri;
    private readonly IHttpClientFactory _clientFactory;

    public SalesRepository(IConfiguration cfg, HttpClient httpClient, IHttpClientFactory clientFactory)
    {
        //_connectionString = cfg.GetConnectionString("NAVDbContext")!;
        _salesUri = cfg.GetValue<string>("SalesApiUri") + "/api/RealTimeData/sales" ?? throw new InvalidOperationException("Sales API URI not configured.");
        _httpClient = httpClient;
        _clientFactory = clientFactory;
    }

    public async Task<List<SalesGroupedByStoreHour>> GetSalesGroupedByStoreHour(Database database)
    {
        var results = new List<SalesGroupedByStoreHour>();

        var databaseName = database.Name;

        var sql = @$"
            with cte_store_categories as (
                select
                    tse.[Store No_] as StoreCode,
                    CONVERT(decimal, COUNT(DISTINCT tse.[Item Category Code])) as CategoryCount
                from [{database.Name}$Trans_ Sales Entry] tse
                inner join [{database.Name}$Transaction Header] th
                    on th.[Store No_] = tse.[Store No_]
                    and th.[POS Terminal No_] = tse.[POS Terminal No_]
                    and th.[Transaction No_] = tse.[Transaction No_]
                where tse.[Date] = CONVERT(date, GETDATE())
                and th.Wastage = 0
                and th.[Transaction Type] = 2
                group by tse.[Store No_]
            ),

            cte_stores_transactions as (

                select
                    th.[Store No_] as [StoreCode],
                    CAST(COUNT(DISTINCT CONCAT(th.[Store No_], th.[POS Terminal No_], th.[Transaction No_])) as decimal) as [TotalTransactions]
                from [{database.Name}$Transaction Header] as th

                where th.[Date] = CONVERT(date,  GETDATE())
                and th.Wastage = 0
                and th.[Transaction Type] = 2

                group by th.[Store No_]
            )


            select s.[Item Store Type] as [Scheme],
                    tse.[Store No_] as [StoreCode],
                    d.[Description] as [DivisionName],
                    ic.[Description] as [CategoryName],
                    MAX(DATEPART(HOUR, tse.[Time])) as [Hour],
                    SUM(tse.[Net Amount] / [BM Rate]) * -1 as [NetAmountAc],
                    AVG(CONVERT(decimal, str.TotalTransactions) / CONVERT(decimal, stc.CategoryCount)) as TotalStoreTransactions,
                    COUNT(DISTINCT CONCAT(tse.[Store No_], tse.[POS Terminal No_], tse.[Transaction No_])) as [TotalTransactions]
            from [{database.Name}$Trans_ Sales Entry] as tse
            left join [{database.Name}$Transaction Header] as th on th.[Store No_] = tse.[Store No_]
                                            and th.[POS Terminal No_] = tse.[POS Terminal No_]
                                            and th.[Transaction No_] = tse.[Transaction No_]
            left join [{database.Name}$Store] as s on s.[No_] = tse.[Store No_]
            left join [{database.Name}$Item Category] as ic on ic.Code = tse.[Item Category Code]
            left join [{database.Name}$Division] as d on d.Code = ic.[Division Code]
            inner join cte_stores_transactions as str on str.StoreCode = tse.[Store No_]
            inner join cte_store_categories as stc on stc.StoreCode = tse.[Store No_]

            where tse.[Date] = CONVERT(date,  GETDATE())
            and th.Wastage = 0
            and th.[Transaction Type] = 2

            group by s.[Item Store Type],
                    tse.[Store No_],
                    d.[Description],
                    ic.[Description]

            order by tse.[Store No_];";

        var sql_old = @$"
            select s.[Item Store Type] as [Scheme],
                    tse.[Store No_] as [StoreCode],
                    d.[Description] as [DivisionName],
                    ic.[Description] as [CategoryName],
                    MAX(DATEPART(HOUR, tse.[Time])) as [Hour],
                    SUM(tse.[Net Amount] / [BM Rate]) * -1 as [NetAmountAc],
                    COUNT(DISTINCT CONCAT(tse.[Store No_], tse.[POS Terminal No_], tse.[Transaction No_])) as [TotalTransactions]
            from [{database.Name}$Trans_ Sales Entry] as tse
            left join [{database.Name}$Transaction Header] as th on th.[Store No_] = tse.[Store No_]
                                            and th.[POS Terminal No_] = tse.[POS Terminal No_]
                                            and th.[Transaction No_] = tse.[Transaction No_]
            left join [{database.Name}$Store] as s on s.[No_] = tse.[Store No_]
            left join [{database.Name}$Item Category] as ic on ic.Code = tse.[Item Category Code]
            left join [{database.Name}$Division] as d on d.Code = ic.[Division Code]

            where tse.[Date] = CONVERT(date,  GETDATE())
            and th.Wastage = 0
            and th.[Transaction Type] = 2

            group by s.[Item Store Type],
                    tse.[Store No_],
                    d.[Description],
                    ic.[Description]

            order by tse.[Store No_];";

        var conntectionString = @$"Server={database.HostIp};
                                    Initial Catalog={database.Name};
                                    Persist Security Info=False;
                                    User ID={database.DefaultLoginUser};
                                    Password={database.DefaultLoginPassword};
                                    MultipleActiveResultSets=False;
                                    Encrypt=True;
                                    TrustServerCertificate=True;
                                    Connection Timeout=30;";


        try
        {

        
            using (var conn = new SqlConnection(conntectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new SalesGroupedByStoreHour
                        {
                            Scheme = reader["Scheme"].ToString() ?? "",
                            StoreCode = reader["StoreCode"].ToString() ?? "",
                            DivisionName = reader["DivisionName"].ToString() ?? "",
                            CategoryName = reader["CategoryName"].ToString() ?? "",
                            Hour = Convert.ToInt32(reader["Hour"]),
                            NetAmountAcy = reader.GetDecimal(reader.GetOrdinal("NetAmountAc")),
                            TotalStoreTransactions = reader.GetDecimal(reader.GetOrdinal("TotalStoreTransactions")),
                            TotalTransactions = reader.GetInt32(reader.GetOrdinal("TotalTransactions"))
                        });
                    }
                }
            }

        }
        catch (SqlException ex)
        {
            Console.WriteLine($"SQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }

        try
        {

        

            _httpClient.DefaultRequestHeaders.Add("X-Source", "SalesRepository");
            _httpClient.DefaultRequestHeaders.Add("X-Company-ID", "GMRL");

            var stores = results.Select(r => r.StoreCode).Distinct().ToList();
        
            foreach(var store in stores)
            {
                var storeResult = results
                    .Where(r => r.StoreCode == store)
                    .ToList();

                var serialized = JsonSerializer.Serialize(storeResult);
                Console.WriteLine(serialized);

                await _httpClient.PostAsJsonAsync("api/RealTimeData/sales", storeResult);
                Console.WriteLine($"------------- Posted sales data for store {store} to {_salesUri}");
            }

        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }


        return results;
    }


    public async Task<List<SalesGroupedByStoreHour>> GetNokNokSalesGroupedByStoreHour(Database database)
    {
        var results = new List<SalesGroupedByStoreHour>();

        var databaseName = database.Name;

        // var sql_old = @$"
        //     select 'NOKNOK' as [Scheme], 
		//             th.[Store No_] as [StoreCode], 
		//             MAX(DATEPART(HOUR, th.[Time])) as [Hour], 
		//             SUM(th.[Net Amount] / [BM Rate]) * -1 as [NetAmountAc],
		//             COUNT(*) as [TotalTransactions]
        //     from [{database.Name}$Transaction Header] as th
        //     left join [{database.Name}$Store] as s on s.[No_] = th.[Store No_]
        //     where th.[Date] = CONVERT(date,  GETDATE())
        //     and th.Wastage = 0
        //     and th.[Transaction Type] = 2

        //     group by s.[Item Store Type], th.[Store No_]

        //     order by [Store No_];";


        var sql = @$"
            select s.[Item Store Type] as [Scheme],
                    tse.[Store No_] as [StoreCode],
                    d.[Description] as [DivisionName],
                    ic.[Description] as [CategoryName],
                    MAX(DATEPART(HOUR, tse.[Time])) as [Hour],
                    SUM(tse.[Net Amount] / [BM Rate]) * -1 as [NetAmountAc],
                    COUNT(*) as [TotalTransactions]
            from [{database.Name}$Trans_ Sales Entry] as tse
            left join [{database.Name}$Transaction Header] as th on th.[Store No_] = tse.[Store No_]
                                            and th.[POS Terminal No_] = tse.[POS Terminal No_]
                                            and th.[Transaction No_] = tse.[Transaction No_]
            left join [{database.Name}$Store] as s on s.[No_] = tse.[Store No_]
            left join [{database.Name}$Item Category] as ic on ic.Code = tse.[Item Category Code]
            left join [{database.Name}$Division] as d on d.Code = ic.[Division Code]

            where tse.[Date] = CONVERT(date,  GETDATE())
            and th.Wastage = 0
            and th.[Transaction Type] = 2

            group by s.[Item Store Type],
                    tse.[Store No_],
                    d.[Description],
                    ic.[Description]

            order by tse.[Store No_];";

        var conntectionString = @$"Server={database.HostIp};
                                    Initial Catalog={database.Name};
                                    Persist Security Info=False;
                                    User ID={database.DefaultLoginUser};
                                    Password={database.DefaultLoginPassword};
                                    MultipleActiveResultSets=False;
                                    Encrypt=True;
                                    TrustServerCertificate=True;
                                    Connection Timeout=30;";


        try
        {

        
            using (var conn = new SqlConnection(conntectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new SalesGroupedByStoreHour
                        {
                            Scheme = reader["Scheme"].ToString() ?? "",
                            StoreCode = reader["StoreCode"].ToString() ?? "",
                            DivisionName = reader["DivisionName"].ToString() ?? "",
                            CategoryName = reader["CategoryName"].ToString() ?? "",
                            Hour = Convert.ToInt32(reader["Hour"]),
                            NetAmountAcy = reader.GetDecimal(reader.GetOrdinal("NetAmountAc")),
                            TotalTransactions = reader.GetInt32(reader.GetOrdinal("TotalTransactions"))
                        });
                    }
                }
            }

        }
        catch (SqlException ex)
        {
            Console.WriteLine($"SQL Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }

        try
        {

        

            _httpClient.DefaultRequestHeaders.Add("X-Source", "SalesRepository");
            _httpClient.DefaultRequestHeaders.Add("X-Company-ID", "GMRL");

            var stores = results.Select(r => r.StoreCode).Distinct().ToList();
        
            foreach(var store in stores)
            {
                var storeResult = results.Where(r => r.StoreCode == store).FirstOrDefault();

                await _httpClient.PostAsJsonAsync("api/RealTimeData/sales", storeResult);
                Console.WriteLine($"------------- Posted sales data for store {store} to {_salesUri}");
            }

        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }


        return results;
    }

    
    public async Task<List<SalesGroupedByStoreHour>> GetNokNokSalesGroupedByStoreHourFO()
    {
        var results = new List<SalesGroupedByStoreHour>();

        ODataResponse<SalesLineFO>? response = new();
        List<string> salesOrderIds = new List<string>();

        string apiClient = "NokNok_D365Api";

        // string parameters = !String.IsNullOrEmpty(top) ? "top=" + top : "";
        // today in 2025-12-19T12:00:00Z format
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd") + "T12:00:00Z";
        Console.WriteLine($"Fetching NokNok FO sales from {today} onwards...");
        string endpoint =  $"SalesOrderLines?$filter=RequestedShippingDate ge {today} and dataAreaId eq 'NKLB'&$select=ShippingWarehouseId,LineAmount&$expand=SalesOrderHeader($select=CurrencyCode,SalesOrderNumber)";


        // print url
        Console.WriteLine($"Using endpoint: {endpoint}");
        // create http client
        using HttpClient client = _clientFactory.CreateClient(apiClient);

        try{
            // make http request
            response = await client.GetFromJsonAsync<ODataResponse<SalesLineFO>>(endpoint);
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error fetching data from NokNok FO API: {ex.Message}");
            throw;
        }

        // print th whole response as text
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(response));

        foreach (var line in response.value)
        {
            results.Add(new SalesGroupedByStoreHour
            {
                Scheme = "NOKNOK",
                StoreCode = line.ShippingWarehouseId,
                Hour = DateTime.UtcNow.Hour,
                NetAmountAcy = line.SalesHeader.CurrencyCode == "LBP" ? line.LineAmount / 89700 : line.LineAmount,
                TotalTransactions = 1,
                SalesOrderNumber = line.SalesHeader.SalesOrderNumber
            });

        }

        // run until NextLink is null
        while (response != null &&!string.IsNullOrEmpty(response.NextLink))
        {
            if (response != null && !string.IsNullOrEmpty(response.NextLink))
            {
                endpoint = response.NextLink;
            }

            Console.WriteLine($"Fetching data from: {endpoint}");

            try{
                // make http request
                response = await client.GetFromJsonAsync<ODataResponse<SalesLineFO>>(endpoint);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error fetching data from NokNok FO API: {ex.Message}");
                throw;
            }

            Console.WriteLine("Processing fetched data...");
            Console.WriteLine($"Fetched {response?.value?.Count ?? 0} records.");

            salesOrderIds.AddRange(response.value.Select(l => l.SalesHeader.SalesOrderNumber));

            if (response != null && response.value != null)
            {
                foreach (var line in response.value)
                {
                    results.Add(new SalesGroupedByStoreHour
                    {
                        Scheme = "NOKNOK",
                        StoreCode = line.ShippingWarehouseId,
                        Hour = DateTime.UtcNow.Hour,
                        NetAmountAcy = line.SalesHeader.CurrencyCode == "LBP" ? line.LineAmount / 89700 : line.LineAmount,
                        TotalTransactions = 1,
                        SalesOrderNumber = line.SalesHeader.SalesOrderNumber
                    });

                }

                
            }
        }

        // print all sales order ids
        foreach (var soId in results.Select(r => r.SalesOrderNumber).Distinct())
        {
            Console.WriteLine($"Processed Sales Order ID: {soId}");
        }

        // remove duplicates from salesOrderIds
        var distinctSalesOrders = results.Select(r => r.SalesOrderNumber).Distinct().ToList();
        var distinctSalesOrderCount = distinctSalesOrders.Count;
        Console.WriteLine($"Total distinct Sales Order IDs: {distinctSalesOrderCount}");

  

        // group the results by StoreCode and Hour and scheme and sum the NetAmountAcy and TotalTransactions
        results = results
            .GroupBy(r => new { r.Scheme, r.StoreCode, r.Hour })
            .Select(g => new SalesGroupedByStoreHour
            {
                Scheme = g.Key.Scheme,
                StoreCode = g.Key.StoreCode,
                DivisionName = "UNKNOWN",
                CategoryName = "UNKNOWN",
                Hour = g.Key.Hour,
                NetAmountAcy = g.Sum(x => x.NetAmountAcy),
                TotalTransactions = g.Where(s => s.StoreCode == g.Key.StoreCode).Select(r => r.SalesOrderNumber).Distinct().Count(),
                TotalStoreTransactions = g.Where(s => s.StoreCode == g.Key.StoreCode).Select(r => r.SalesOrderNumber).Distinct().Count()
            })
            .ToList();

        try
        {
            _httpClient.DefaultRequestHeaders.Add("X-Source", "SalesRepository");
            _httpClient.DefaultRequestHeaders.Add("X-Company-ID", "GMRL");


            


            var stores = results.Select(r => r.StoreCode).Distinct().ToList();
        
            foreach(var store in stores)
            {
                var storeResult = results.Where(r => r.StoreCode == store);

                await _httpClient.PostAsJsonAsync("api/RealTimeData/sales", storeResult);
                Console.WriteLine($"------------- Posted sales data for store {store} to {_salesUri}");
            }

        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Error: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            throw;
        }


        return results;
    }

    



}


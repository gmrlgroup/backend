using Application.Scheduler.Models;
using Application.Shared.Models.Data;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Application.Scheduler.Repositories;

public interface ISalesRepository
{
    Task<List<SalesGroupedByStoreHour>> GetSalesGroupedByStoreHour(Database database);
}


public class SalesRepository : ISalesRepository
{
    //private readonly string _connectionString;
    private readonly HttpClient _httpClient;
    private readonly string _salesUri;

    public SalesRepository(IConfiguration cfg, HttpClient httpClient)
    {
        //_connectionString = cfg.GetConnectionString("NAVDbContext")!;
        _salesUri = cfg.GetValue<string>("SalesApiUri") + "api/RealTimeData/sales" ?? throw new InvalidOperationException("Sales API URI not configured.");
        _httpClient = httpClient;
    }

    public async Task<List<SalesGroupedByStoreHour>> GetSalesGroupedByStoreHour(Database database)
    {
        var results = new List<SalesGroupedByStoreHour>();

        var databaseName = database.Name;

        var sql = @$"
            select s.[Item Store Type] as [Scheme], 
		            th.[Store No_] as [StoreCode], 
		            MAX(DATEPART(HOUR, th.[Time])) as [Hour], 
		            SUM(th.[Net Amount] / [BM Rate]) * -1 as [NetAmountAc],
		            COUNT(*) as [TotalTransactions]
            from [{database.Name}$Transaction Header] as th
            left join [{database.Name}$Store] as s on s.[No_] = th.[Store No_]
            where th.[Date] = CONVERT(date,  GETDATE())
            and th.Wastage = 0
            and th.[Transaction Type] = 2

            group by s.[Item Store Type], th.[Store No_]

            order by [Store No_];";

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
}


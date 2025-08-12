using Application.Shared.Services.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Security.Claims;
using System.Text.Json;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using System.Net.Http.Json;
using System.Web;

namespace Application.Client.Pages.Data;

public partial class RealTimeSalesDashboard : IAsyncDisposable
{
    private HubConnection? hubConnection;
    private bool isLoading = true;
    private bool showColumnSettings = false;
    private DateTime? lastUpdated;

    [SupplyParameterFromQuery(Name = "c")]
    public string? companyId { get; set; }

    private SalesKpiData? overallKpi;
    private List<SalesBannerKpi>? bannerKpis;
    private List<SalesDashboardData>? dashboardData;

    [Inject]
    public HttpClient HttpClient { get; set; } = default!;

    private ColumnSettings columnSettings = new()
    {
        ShowScheme = true,
        ShowStoreCode = true,
        ShowTotalSales = true,
        ShowTotalTransactions = true,
        ShowAverageBasket = true,
        ShowLastUpdated = true
    };

    public bool IsConnected =>
        hubConnection?.State == HubConnectionState.Connected;

    protected override async Task OnInitializedAsync()
    {

        await InitializeSignalRConnection();
        await LoadInitialData();

        try
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (user.Identity?.IsAuthenticated == true)
            {
                // Get company ID from query parameter or user claims
                var uri = new Uri(NavigationManager.Uri);
                //var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                //companyId = query["companyId"] ?? user.FindFirst("companyId")?.Value;

                if (!string.IsNullOrEmpty(companyId))
                {
                    await InitializeSignalRConnection();
                    await LoadInitialData();
                }
            }
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error initializing dashboard: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task InitializeSignalRConnection()
    {
        try
        {
            Console.WriteLine("Initializing SignalR connection...");
            
            hubConnection = new HubConnectionBuilder()
                .WithUrl(NavigationManager.ToAbsoluteUri("/realtime/salesdata"))
                .Build();

            //hubConnection.On<Notification<SalesData>>("ReceiveSalesData", OnSalesDataReceived);

            hubConnection.On<Notification<SalesData>>("ReceiveSalesData", async (notification) =>
            {
                if (notification?.Data == null) return;
                
                try
                {
                    // Debug logging
                    Console.WriteLine($"Received SignalR data: Store {notification.Data.StoreCode}, Sales {notification.Data.NetAmountAcy}");
                    
                    // Update or add the store data
                    if (dashboardData == null)
                        dashboardData = new List<SalesDashboardData>();

                    var existingStore = dashboardData.FirstOrDefault(d => 
                        d.Scheme == notification.Data.Scheme && 
                        d.StoreCode == notification.Data.StoreCode);

                    if (existingStore != null)
                    {
                        //var existingNetAmount = existingStore.TotalSales;
                        //var existingTransactions = existingStore.TotalTransactions;
                        // Update existing store data
                        existingStore.TotalSales = notification.Data.NetAmountAcy; // - existingNetAmount;
                        existingStore.TotalTransactions = notification.Data.TotalTransactions; // - existingTransactions;
                        existingStore.LastUpdated = DateTime.Now;
                        Console.WriteLine($"Updated existing store: {existingStore.StoreCode}");
                    }
                    else
                    {
                        // Add new store data
                        var newDashboardData = new SalesDashboardData()
                        {
                            Scheme = notification.Data.Scheme,
                            StoreCode = notification.Data.StoreCode,
                            TotalSales = notification.Data.NetAmountAcy,
                            TotalTransactions = notification.Data.TotalTransactions,
                            LastUpdated = DateTime.Now
                        };
                        dashboardData.Add(newDashboardData);
                        Console.WriteLine($"Added new store: {newDashboardData.StoreCode}");
                    }

                    // Recalculate overall KPIs
                    if (overallKpi == null)
                        overallKpi = new SalesKpiData();

                    overallKpi.TotalSales = dashboardData.Sum(d => d.TotalSales);
                    overallKpi.TotalTransactions = dashboardData.Sum(d => d.TotalTransactions);
                    overallKpi.TotalStores = dashboardData.Count;
                    overallKpi.LastUpdated = DateTime.Now;

                    // Update banner KPIs
                    bannerKpis = dashboardData
                        .GroupBy(d => d.Scheme)
                        .Select(g => new SalesBannerKpi
                        {
                            Banner = g.Key ?? "Unknown",
                            TotalSales = g.Sum(x => x.TotalSales),
                            TotalTransactions = g.Sum(x => x.TotalTransactions),
                            StoreCount = g.Count()
                        })
                        .ToList();

                    lastUpdated = DateTime.Now;
                    
                    Console.WriteLine($"Updated dashboard: Total stores: {overallKpi.TotalStores}, Total sales: {overallKpi.TotalSales}");
                    
                    await InvokeAsync(StateHasChanged);
                    
                    // Show a toast notification for debugging
                    //await InvokeAsync(() => 
                    //    ToastService.ShowInfo($"New sales data: {notification.Data.StoreCode} - {notification.Data.NetAmountAcy:C}"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing SignalR data: {ex.Message}");
                    await InvokeAsync(() => 
                        ToastService.ShowError($"Error processing real-time data: {ex.Message}"));
                }
            });

            await hubConnection.StartAsync();
            Console.WriteLine("SignalR connection started successfully");

            if (!string.IsNullOrEmpty(companyId))
            {
                await hubConnection.InvokeAsync("JoinCompanyGroup", companyId);
                Console.WriteLine($"Joined company group: {companyId}");
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SignalR connection error: {ex.Message}");
            ToastService.ShowError($"Error connecting to real-time updates: {ex.Message}");
        }
    }

    //private async Task OnSalesDataReceived(Notification<SalesData> notification)
    //{
    //    try
    //    {
    //        ToastService.ShowInfo($"New sales data: {notification.Message}");
    //        await LoadData();
    //        lastUpdated = DateTime.Now;
    //        StateHasChanged();
    //    }
    //    catch (Exception ex)
    //    {
    //        ToastService.ShowError($"Error processing real-time data: {ex.Message}");
    //    }
    //}

    private async Task LoadInitialData()
    {
        await LoadData();
        lastUpdated = DateTime.Now;
    }

    private async Task LoadData()
    {
        try
        {
            //if (string.IsNullOrEmpty(companyId)) return;

            HttpClient.DefaultRequestHeaders.Clear();
            HttpClient.DefaultRequestHeaders.Add("X-Company-ID", companyId);

            var kpiTask = await HttpClient.GetFromJsonAsync<SalesKpiData>("/api/salesdashboard/kpi");
            var bannerTask = await HttpClient.GetFromJsonAsync<List<SalesBannerKpi>>("/api/salesdashboard/banner-kpi");
            var dataTask = await HttpClient.GetFromJsonAsync<List<SalesDashboardData>>("/api/salesdashboard/data");

            //await Task.WhenAll(kpiTask, bannerTask, dataTask);

            overallKpi = kpiTask;
            bannerKpis = bannerTask;
            dashboardData = dataTask;
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error loading dashboard data: {ex.Message}");
        }
    }

    private async Task RefreshData()
    {
        isLoading = true;
        StateHasChanged();

        try
        {
            await LoadData();
            lastUpdated = DateTime.Now;
            ToastService.ShowSuccess("Dashboard data refreshed");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Error refreshing data: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void ToggleColumnSettings()
    {
        showColumnSettings = !showColumnSettings;
        StateHasChanged();
    }

    private string GetGridTemplateColumns()
    {
        var columns = new List<string>();
        
        if (columnSettings.ShowScheme) columns.Add("1fr");
        if (columnSettings.ShowStoreCode) columns.Add("1fr");
        if (columnSettings.ShowTotalSales) columns.Add("120px");
        if (columnSettings.ShowTotalTransactions) columns.Add("120px");
        if (columnSettings.ShowAverageBasket) columns.Add("120px");
        if (columnSettings.ShowLastUpdated) columns.Add("100px");

        return string.Join(" ", columns);
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            try
            {
                if (!string.IsNullOrEmpty(companyId))
                {
                    await hubConnection.InvokeAsync("LeaveCompanyGroup", companyId);
                }
            }
            catch (Exception)
            {
                // Ignore errors when leaving group during disposal
            }

            await hubConnection.DisposeAsync();
        }
    }

    public class ColumnSettings
    {
        public bool ShowScheme { get; set; } = true;
        public bool ShowStoreCode { get; set; } = true;
        public bool ShowTotalSales { get; set; } = true;
        public bool ShowTotalTransactions { get; set; } = true;
        public bool ShowAverageBasket { get; set; } = true;
        public bool ShowLastUpdated { get; set; } = true;
    }
}

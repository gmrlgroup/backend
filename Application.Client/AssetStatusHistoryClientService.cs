using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

public class AssetStatusHistoryClientService
{
    private readonly HttpClient _httpClient;

    public AssetStatusHistoryClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    public async Task<List<AssetStatusHistory>> GetEntityStatusHistoryAsync(string entityId, string companyId, int days = 7)
    {
        try
        {
            SetCompanyHeader(companyId);
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var toDate = DateTime.UtcNow;
            var response = await _httpClient.GetAsync(
                $"api/status/history/entity/{entityId}?fromDate={fromDate:yyyy-MM-ddTHH:mm:ss.fffZ}&toDate={toDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AssetStatusHistory>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching entity status history: {ex.Message}"); return new(); }
    }

    public async Task<AssetStatusHistory?> GetLatestEntityStatusAsync(string entityId, string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/history/entity/{entityId}/latest");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssetStatusHistory>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching latest entity status: {ex.Message}"); return null; }
    }

    public async Task<Dictionary<string, int>> GetEntityStatusSummaryAsync(string entityId, string companyId, int days = 30)
    {
        try
        {
            SetCompanyHeader(companyId);
            var fromDate = DateTime.UtcNow.AddDays(-days);
            var toDate = DateTime.UtcNow;
            var response = await _httpClient.GetAsync(
                $"api/status/history/entity/{entityId}/summary?fromDate={fromDate:yyyy-MM-ddTHH:mm:ss.fffZ}&toDate={toDate:yyyy-MM-ddTHH:mm:ss.fffZ}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Dictionary<string, int>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching entity status summary: {ex.Message}"); return new(); }
    }
}

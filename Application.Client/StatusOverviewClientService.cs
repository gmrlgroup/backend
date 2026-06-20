using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>Client-side access to the public status board (overview + per-day events).</summary>
public class StatusOverviewClientService
{
    private readonly HttpClient _httpClient;

    public StatusOverviewClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    public async Task<StatusOverviewDto?> GetOverviewAsync(string companyId, int days = 30)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/overview?days={days}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<StatusOverviewDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching status overview: {ex.Message}"); return null; }
    }

    public async Task<List<StatusDayEventDto>> GetDayEventsAsync(string companyId, string entityId, DateTime dateUtc)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync(
                $"api/status/overview/entities/{entityId}/day?date={dateUtc:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<StatusDayEventDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching day events: {ex.Message}"); return new(); }
    }
}

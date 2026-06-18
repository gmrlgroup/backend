using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

public class IncidentClientService
{
    private readonly HttpClient _httpClient;

    public IncidentClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    public async Task<List<Incident>> GetIncidentsAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/incidents");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<Incident>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching incidents: {ex.Message}"); return new(); }
    }

    public async Task<PagedResult<Incident>> GetIncidentsPagedAsync(string companyId, IncidentQueryParameters p)
    {
        try
        {
            SetCompanyHeader(companyId);

            var query = new List<string>
            {
                $"page={p.Page}",
                $"pageSize={p.PageSize}",
                $"sortBy={Uri.EscapeDataString(p.SortBy)}",
                $"sortDir={Uri.EscapeDataString(p.SortDir)}"
            };
            if (!string.IsNullOrWhiteSpace(p.Search)) query.Add($"search={Uri.EscapeDataString(p.Search)}");
            if (p.Severity.HasValue) query.Add($"severity={p.Severity.Value}");
            if (p.Status.HasValue) query.Add($"status={p.Status.Value}");
            if (p.ActiveOnly) query.Add("activeOnly=true");

            var response = await _httpClient.GetAsync($"api/status/incidents/paged?{string.Join("&", query)}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<PagedResult<Incident>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching paged incidents: {ex.Message}"); return new(); }
    }

    public async Task<List<Incident>> GetActiveIncidentsAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/incidents/active");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<Incident>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching active incidents: {ex.Message}"); return new(); }
    }

    public async Task<List<Incident>> GetIncidentsByEntityAsync(string entityId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/incidents/entity/{entityId}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<Incident>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching incidents by entity: {ex.Message}"); return new(); }
    }

    public async Task<Incident?> GetIncidentAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/incidents/{id}");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Incident>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching incident: {ex.Message}"); return null; }
    }

    public async Task<Incident?> CreateIncidentAsync(string companyId, Incident incident)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync("api/status/incidents", incident);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Incident>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error creating incident: {ex.Message}"); return null; }
    }

    public async Task<List<IncidentUpdate>> GetIncidentUpdatesAsync(string incidentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/incidents/{incidentId}/updates");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<IncidentUpdate>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching incident updates: {ex.Message}"); return new(); }
    }

    public async Task<bool> UpdateIncidentAsync(Incident incident)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/status/incidents/{incident.Id}", incident);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating incident: {ex.Message}"); return false; }
    }

    public async Task<IncidentUpdate?> AddIncidentUpdateAsync(string incidentId, IncidentUpdate update)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/status/incidents/{incidentId}/updates", update);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<IncidentUpdate>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error adding incident update: {ex.Message}"); return null; }
    }
}

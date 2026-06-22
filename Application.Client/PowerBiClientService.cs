using Application.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>Client-side access to Power BI connection CRUD, dataset link config, and refresh endpoints.</summary>
public class PowerBiClientService
{
    private readonly HttpClient _httpClient;

    public PowerBiClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    // ---- Connections ----

    public async Task<List<PowerBiConnectionDto>> GetConnectionsAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/powerbi/connections");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<PowerBiConnectionDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching Power BI connections: {ex.Message}"); return new(); }
    }

    public async Task<PowerBiConnectionDto?> CreateConnectionAsync(string companyId, PowerBiConnectionRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync("api/status/powerbi/connections", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PowerBiConnectionDto>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error creating Power BI connection: {ex.Message}"); return null; }
    }

    public async Task<bool> UpdateConnectionAsync(string companyId, string connectionId, PowerBiConnectionRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PutAsJsonAsync($"api/status/powerbi/connections/{connectionId}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating Power BI connection: {ex.Message}"); return false; }
    }

    /// <summary>Returns null on success, or an error message (e.g. the connection is still linked to a dataset).</summary>
    public async Task<string?> DeleteConnectionAsync(string companyId, string connectionId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/powerbi/connections/{connectionId}");
            if (response.IsSuccessStatusCode) return null;
            var message = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(message) ? $"Delete failed ({(int)response.StatusCode})." : message;
        }
        catch (Exception ex) { return ex.Message; }
    }

    // ---- Dataset link ----

    public async Task<PowerBiDatasetLinkDto?> GetLinkAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/powerbi/link");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<PowerBiDatasetLinkDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching Power BI link: {ex.Message}"); return null; }
    }

    /// <summary>Returns the saved link, or throws with the server's message on failure.</summary>
    public async Task<PowerBiDatasetLinkDto?> SaveLinkAsync(string companyId, string entityId, PowerBiDatasetLinkRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PutAsJsonAsync($"api/status/entities/{entityId}/powerbi/link", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PowerBiDatasetLinkDto>();

        var message = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"Save failed ({(int)response.StatusCode})." : message);
    }

    public async Task<bool> DeleteLinkAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/entities/{entityId}/powerbi/link");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting Power BI link: {ex.Message}"); return false; }
    }

    // ---- Refresh history + trigger ----

    /// <summary>Fetches refresh history, or throws with the server's message on failure.</summary>
    public async Task<List<PowerBiRefreshDto>> GetRefreshHistoryAsync(string companyId, string entityId, int top = 20)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/powerbi/refreshes?top={top}");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<PowerBiRefreshDto>>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<PowerBiActionResult>();
        throw new InvalidOperationException(error?.Message ?? $"Failed to load refresh history ({(int)response.StatusCode}).");
    }

    /// <summary>Fetches the dataset's refresh schedule (with computed next run); null when none is configured.</summary>
    public async Task<PowerBiRefreshScheduleDto?> GetRefreshScheduleAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/powerbi/schedule");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<PowerBiRefreshScheduleDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching Power BI schedule: {ex.Message}"); return null; }
    }

    // ---- Lineage discovery ----

    /// <summary>Discovers the dataset's databases + tables, or throws with the server's message on failure.</summary>
    public async Task<PowerBiDiscoveryDto> GetDiscoveryAsync(string companyId, string entityId)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/powerbi/discover");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PowerBiDiscoveryDto>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<PowerBiActionResult>();
        throw new InvalidOperationException(error?.Message ?? $"Discovery failed ({(int)response.StatusCode}).");
    }

    /// <summary>Commits the chosen databases + tables as entities and dependencies; throws with the server's message on failure.</summary>
    public async Task<PowerBiLineageCommitResult> CommitLineageAsync(string companyId, string entityId, PowerBiLineageCommitRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PostAsJsonAsync($"api/status/entities/{entityId}/powerbi/lineage", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<PowerBiLineageCommitResult>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<PowerBiActionResult>();
        throw new InvalidOperationException(error?.Message ?? $"Commit failed ({(int)response.StatusCode}).");
    }

    public async Task<PowerBiActionResult> TriggerRefreshAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsync($"api/status/entities/{entityId}/powerbi/refresh", null);
            var result = await response.Content.ReadFromJsonAsync<PowerBiActionResult>();
            return result ?? PowerBiActionResult.Fail($"Refresh failed ({(int)response.StatusCode}).");
        }
        catch (Exception ex) { return PowerBiActionResult.Fail(ex.Message); }
    }
}

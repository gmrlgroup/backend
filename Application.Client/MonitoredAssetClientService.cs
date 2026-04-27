using Application.Shared.Enums;
using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

public class MonitoredAssetClientService
{
    private readonly HttpClient _httpClient;

    public MonitoredAssetClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    public async Task<List<MonitoredAsset>> GetEntitiesAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/entities");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<MonitoredAsset>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching entities: {ex.Message}"); return new(); }
    }

    public async Task<List<MonitoredAsset>> GetEntitiesWithLatestStatusAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/entities/with-latest-status");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<MonitoredAsset>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching entities with status: {ex.Message}"); return new(); }
    }

    public async Task<MonitoredAsset?> GetEntityAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/entities/{id}");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonitoredAsset>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching entity: {ex.Message}"); return null; }
    }

    public async Task<MonitoredAsset?> CreateEntityAsync(string companyId, MonitoredAsset entity)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync("api/status/entities", entity);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MonitoredAsset>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error creating entity: {ex.Message}"); return null; }
    }

    public async Task<bool> UpdateEntityAsync(MonitoredAsset entity)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/status/entities/{entity.Id}", entity);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating entity: {ex.Message}"); return false; }
    }

    public async Task<bool> DeleteEntityAsync(string id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/status/entities/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting entity: {ex.Message}"); return false; }
    }

    public async Task<Dictionary<string, int>> GetEntityStatusSummaryAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/status/entities/summary");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Dictionary<string, int>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching summary: {ex.Message}"); return new(); }
    }

    public async Task<List<AssetDependency>> GetEntityDependenciesAsync(string entityId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/dependencies");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AssetDependency>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching dependencies: {ex.Message}"); return new(); }
    }

    public async Task<AssetDependencyTree?> GetEntityDependencyTreeAsync(string entityId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/dependency-tree");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssetDependencyTree>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching dependency tree: {ex.Message}"); return null; }
    }

    public async Task<AssetDependency?> AddDependencyAsync(string entityId, AssetDependency dependency)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/status/entities/{entityId}/dependencies", dependency);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AssetDependency>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error adding dependency: {ex.Message}"); return null; }
    }

    public async Task<bool> RemoveDependencyAsync(string dependencyId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/status/entities/dependencies/{dependencyId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error removing dependency: {ex.Message}"); return false; }
    }

    public async Task<bool> UpdateDependencyAsync(AssetDependency dependency)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/status/entities/dependencies/{dependency.Id}", dependency);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating dependency: {ex.Message}"); return false; }
    }

    public async Task<bool> UpdateEntityStatusAsync(string entityId, AssetStatus status, string statusMessage)
    {
        try
        {
            var request = new { Status = status, StatusMessage = statusMessage, EntityId = entityId };
            var response = await _httpClient.PostAsJsonAsync($"api/status/entities/{entityId}/status", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating entity status: {ex.Message}"); return false; }
    }
}

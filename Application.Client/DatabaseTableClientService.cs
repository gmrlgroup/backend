using Application.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>Client-side access to a Database entity's connection config and table discovery/commit.</summary>
public class DatabaseTableClientService
{
    private readonly HttpClient _httpClient;

    public DatabaseTableClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    // ---- Connection ----

    public async Task<DatabaseConnectionDto?> GetConnectionAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/database/connection");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DatabaseConnectionDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching database connection: {ex.Message}"); return null; }
    }

    /// <summary>Saves the connection, or throws with the server's message on failure.</summary>
    public async Task<DatabaseConnectionDto?> SaveConnectionAsync(string companyId, string entityId, DatabaseConnectionRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PutAsJsonAsync($"api/status/entities/{entityId}/database/connection", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DatabaseConnectionDto>();

        var message = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"Save failed ({(int)response.StatusCode})." : message);
    }

    public async Task<bool> DeleteConnectionAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/entities/{entityId}/database/connection");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting database connection: {ex.Message}"); return false; }
    }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsync($"api/status/entities/{entityId}/database/connection/test", null);
            var result = await response.Content.ReadFromJsonAsync<DatabaseConnectionTestResult>();
            return result ?? new DatabaseConnectionTestResult { Ok = false, Error = $"Test failed ({(int)response.StatusCode})." };
        }
        catch (Exception ex) { return new DatabaseConnectionTestResult { Ok = false, Error = ex.Message }; }
    }

    // ---- Tables ----

    /// <summary>Discovers the database's tables, or throws with the server's message on failure.</summary>
    public async Task<DatabaseTableDiscoveryDto> DiscoverTablesAsync(string companyId, string entityId)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/database/tables");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DatabaseTableDiscoveryDto>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<DatabaseConnectionTestResult>();
        throw new InvalidOperationException(error?.Error ?? $"Discovery failed ({(int)response.StatusCode}).");
    }

    /// <summary>Commits the chosen tables as entities and dependencies; throws with the server's message on failure.</summary>
    public async Task<DatabaseTableCommitResult> CommitTablesAsync(string companyId, string entityId, DatabaseTableCommitRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PostAsJsonAsync($"api/status/entities/{entityId}/database/tables", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DatabaseTableCommitResult>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<DatabaseConnectionTestResult>();
        throw new InvalidOperationException(error?.Error ?? $"Commit failed ({(int)response.StatusCode}).");
    }

    // ---- Table freshness check ----

    public async Task<TableCheckDto?> GetTableCheckAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/table/check");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<TableCheckDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching table check: {ex.Message}"); return null; }
    }

    /// <summary>Saves the freshness check, or throws with the server's message on failure.</summary>
    public async Task<TableCheckDto?> SaveTableCheckAsync(string companyId, string entityId, TableCheckRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PutAsJsonAsync($"api/status/entities/{entityId}/table/check", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<TableCheckDto>();

        var message = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"Save failed ({(int)response.StatusCode})." : message);
    }

    public async Task<bool> DeleteTableCheckAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/entities/{entityId}/table/check");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting table check: {ex.Message}"); return false; }
    }

    public async Task<TableFreshnessResult> RunTableCheckAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsync($"api/status/entities/{entityId}/table/check/run", null);
            var result = await response.Content.ReadFromJsonAsync<TableFreshnessResult>();
            return result ?? new TableFreshnessResult { Ok = false, Error = $"Run failed ({(int)response.StatusCode})." };
        }
        catch (Exception ex) { return new TableFreshnessResult { Ok = false, Error = ex.Message }; }
    }
}

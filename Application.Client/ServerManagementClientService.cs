using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>Client-side access to server credential CRUD and service start/stop endpoints.</summary>
public class ServerManagementClientService
{
    private readonly HttpClient _httpClient;

    public ServerManagementClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    // ---- Credentials ----

    public async Task<List<ServerCredentialDto>> GetCredentialsAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/credentials");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<ServerCredentialDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching credentials: {ex.Message}"); return new(); }
    }

    public async Task<ServerCredentialDto?> CreateCredentialAsync(string companyId, string entityId, ServerCredentialRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync($"api/status/entities/{entityId}/credentials", request);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ServerCredentialDto>() : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error creating credential: {ex.Message}"); return null; }
    }

    public async Task<bool> UpdateCredentialAsync(string companyId, string entityId, string credentialId, ServerCredentialRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PutAsJsonAsync($"api/status/entities/{entityId}/credentials/{credentialId}", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating credential: {ex.Message}"); return false; }
    }

    public async Task<bool> DeleteCredentialAsync(string companyId, string entityId, string credentialId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/entities/{entityId}/credentials/{credentialId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting credential: {ex.Message}"); return false; }
    }

    // ---- Services ----

    public async Task<List<RemoteServiceInfo>> DiscoverServicesAsync(string companyId, string entityId, string? credentialId = null)
    {
        SetCompanyHeader(companyId);
        var url = $"api/status/entities/{entityId}/services";
        if (!string.IsNullOrEmpty(credentialId)) url += $"?credentialId={Uri.EscapeDataString(credentialId)}";

        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<RemoteServiceInfo>>() ?? new();

        var error = await response.Content.ReadFromJsonAsync<ServiceActionResult>();
        throw new InvalidOperationException(error?.Message ?? $"Discovery failed ({(int)response.StatusCode}).");
    }

    public Task<ServiceActionResult> StartServiceAsync(string companyId, string entityId, string serviceName, string? credentialId = null)
        => PostActionAsync(companyId, entityId, serviceName, "start", credentialId);

    public Task<ServiceActionResult> StopServiceAsync(string companyId, string entityId, string serviceName, string? credentialId = null)
        => PostActionAsync(companyId, entityId, serviceName, "stop", credentialId);

    private async Task<ServiceActionResult> PostActionAsync(string companyId, string entityId, string serviceName, string action, string? credentialId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var url = $"api/status/entities/{entityId}/services/{Uri.EscapeDataString(serviceName)}/{action}";
            if (!string.IsNullOrEmpty(credentialId)) url += $"?credentialId={Uri.EscapeDataString(credentialId)}";

            var response = await _httpClient.PostAsync(url, null);
            var result = await response.Content.ReadFromJsonAsync<ServiceActionResult>();
            return result ?? ServiceActionResult.Fail($"{action} failed ({(int)response.StatusCode}).");
        }
        catch (Exception ex) { return ServiceActionResult.Fail(ex.Message); }
    }
}

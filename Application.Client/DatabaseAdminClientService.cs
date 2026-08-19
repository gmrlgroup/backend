using Application.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>Client-side access to the Database Management API: space usage and user provisioning.</summary>
public class DatabaseAdminClientService
{
    private readonly HttpClient _httpClient;

    public DatabaseAdminClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    // ---- Databases ----

    public async Task<List<DatabaseAdminEntityDto>> GetDatabasesAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/database-admin/databases");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<DatabaseAdminEntityDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching databases: {ex.Message}"); return new(); }
    }

    // ---- Admin credential ----

    public async Task<DatabaseAdminCredentialDto?> GetCredentialAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/database-admin/databases/{entityId}/credential");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DatabaseAdminCredentialDto>();
            return null;
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching admin credential: {ex.Message}"); return null; }
    }

    /// <summary>Saves the admin credential, or throws with the server's message on failure.</summary>
    public async Task<DatabaseAdminCredentialDto?> SaveCredentialAsync(string companyId, string entityId, DatabaseAdminCredentialRequest request)
    {
        SetCompanyHeader(companyId);
        var response = await _httpClient.PutAsJsonAsync($"api/database-admin/databases/{entityId}/credential", request);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<DatabaseAdminCredentialDto>();

        var message = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"Save failed ({(int)response.StatusCode})." : message);
    }

    public async Task<bool> DeleteCredentialAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/database-admin/databases/{entityId}/credential");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error deleting admin credential: {ex.Message}"); return false; }
    }

    // ---- Size ----

    public async Task<DatabaseSizeDto> GetSizeAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/database-admin/databases/{entityId}/size");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DatabaseSizeDto>() ?? new();
            return new DatabaseSizeDto { Error = $"Size check failed ({(int)response.StatusCode})." };
        }
        catch (Exception ex) { return new DatabaseSizeDto { Error = ex.Message }; }
    }

    // ---- Users ----

    public async Task<DatabaseUserListDto> GetUsersAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/database-admin/databases/{entityId}/users");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DatabaseUserListDto>() ?? new();
            return new DatabaseUserListDto { Error = $"Could not list users ({(int)response.StatusCode})." };
        }
        catch (Exception ex) { return new DatabaseUserListDto { Error = ex.Message }; }
    }

    public Task<DatabaseUserOperationResult> CreateUserAsync(string companyId, string entityId, CreateDatabaseUserRequest request) =>
        PostUserOperationAsync(companyId, $"api/database-admin/databases/{entityId}/users", request);

    public Task<DatabaseUserOperationResult> ResetPasswordAsync(string companyId, string entityId, ResetDatabaseUserPasswordRequest request) =>
        PostUserOperationAsync(companyId, $"api/database-admin/databases/{entityId}/users/reset-password", request);

    public async Task<DatabaseUserOperationResult> DropUserAsync(string companyId, string entityId, DropDatabaseUserRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            // DELETE with a body — HttpClient.DeleteAsync has no overload for one, so build the request.
            var message = new HttpRequestMessage(HttpMethod.Delete, $"api/database-admin/databases/{entityId}/users")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _httpClient.SendAsync(message);
            return await ReadOperationResultAsync(response, request.Username);
        }
        catch (Exception ex) { return new DatabaseUserOperationResult { Ok = false, Username = request.Username, Error = ex.Message }; }
    }

    private async Task<DatabaseUserOperationResult> PostUserOperationAsync<TRequest>(string companyId, string url, TRequest request)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync(url, request);
            return await ReadOperationResultAsync(response, null);
        }
        catch (Exception ex) { return new DatabaseUserOperationResult { Ok = false, Error = ex.Message }; }
    }

    /// <summary>The server returns the same result shape for both success and handled failure (400).</summary>
    private static async Task<DatabaseUserOperationResult> ReadOperationResultAsync(HttpResponseMessage response, string? username)
    {
        try
        {
            var result = await response.Content.ReadFromJsonAsync<DatabaseUserOperationResult>();
            if (result != null) return result;
        }
        catch
        {
            // Fall through to the status-code message below.
        }

        return new DatabaseUserOperationResult
        {
            Ok = false,
            Username = username ?? string.Empty,
            Error = $"Request failed ({(int)response.StatusCode})."
        };
    }
}

using Application.Shared.Models;
using Application.Shared.Models.Logging;
using System.Net.Http.Json;

namespace Application.Client.Services;

/// <summary>
/// Client wrapper for the per-company ADMIN settings (<c>api/company-settings</c> — debug-logging toggle and
/// CSV export date format) and the debug-log reader (<c>api/data-log?source=debug</c>).
/// Follows the app convention of setting the <c>X-Company-Id</c> header per call.
/// </summary>
public class DebugLogClientService
{
    private readonly HttpClient _httpClient;

    public DebugLogClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    private void SetUserHeader(string? userId)
    {
        _httpClient.DefaultRequestHeaders.Remove("UserId");
        if (!string.IsNullOrEmpty(userId))
            _httpClient.DefaultRequestHeaders.Add("UserId", userId);
    }

    public async Task<CompanySettingsDto?> GetSettingsAsync(string companyId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync("api/company-settings");
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<CompanySettingsDto>()
                : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Saves the company's settings, returning the saved state (null on failure so the caller can revert).
    /// Pass the current value of every field you aren't changing — a null <c>ExportDateFormat</c> is the
    /// API's "leave unchanged" signal, but <c>DebugLoggingEnabled</c> is a bool and is always applied.
    /// </summary>
    public async Task<CompanySettingsDto?> SaveSettingsAsync(string companyId, string? userId, CompanySettingsDto settings)
    {
        try
        {
            SetCompanyHeader(companyId);
            SetUserHeader(userId);
            var response = await _httpClient.PutAsJsonAsync("api/company-settings", settings);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<CompanySettingsDto>()
                : null;
        }
        catch { return null; }
    }

    public async Task<bool> SetDebugLoggingAsync(string companyId, string? userId, bool enabled)
    {
        try
        {
            SetCompanyHeader(companyId);
            SetUserHeader(userId);
            var response = await _httpClient.PutAsJsonAsync(
                "api/company-settings", new CompanySettingsDto { DebugLoggingEnabled = enabled });
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<DataAppLogQueryResult> QueryDebugLogAsync(string companyId, int pageSize = 100)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/data-log?source=debug&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DataAppLogQueryResult>() ?? new();
            return new();
        }
        catch { return new(); }
    }
}

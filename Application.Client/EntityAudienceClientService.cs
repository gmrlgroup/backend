using Application.Shared.Enums;
using Application.Shared.Models;
using System.Net.Http.Json;

namespace Application.Client.Services;

public class EntityAudienceClientService
{
    private readonly HttpClient _httpClient;

    public EntityAudienceClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    private void SetCompanyHeader(string companyId)
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Company-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Company-Id", companyId);
    }

    public async Task<List<EntityAudience>> GetAudienceAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/audience");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<EntityAudience>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching audience: {ex.Message}"); return new(); }
    }

    public async Task<List<AssignableUser>> GetAssignableUsersAsync(string companyId, string entityId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.GetAsync($"api/status/entities/{entityId}/audience/assignable-users");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AssignableUser>>() ?? new();
            return new();
        }
        catch (Exception ex) { Console.WriteLine($"Error fetching assignable users: {ex.Message}"); return new(); }
    }

    public async Task<EntityAudience?> AddAsync(string companyId, string entityId, string applicationUserId, EntityAudienceType type)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PostAsJsonAsync(
                $"api/status/entities/{entityId}/audience",
                new { ApplicationUserId = applicationUserId, AudienceType = type });
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<EntityAudience>()
                : null;
        }
        catch (Exception ex) { Console.WriteLine($"Error adding audience: {ex.Message}"); return null; }
    }

    public async Task<bool> UpdateAsync(string companyId, EntityAudience audience)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.PutAsJsonAsync($"api/status/entities/audience/{audience.Id}", audience);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error updating audience: {ex.Message}"); return false; }
    }

    public async Task<bool> RemoveAsync(string companyId, string audienceId)
    {
        try
        {
            SetCompanyHeader(companyId);
            var response = await _httpClient.DeleteAsync($"api/status/entities/audience/{audienceId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { Console.WriteLine($"Error removing audience: {ex.Message}"); return false; }
    }

    public class AssignableUser
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
    }
}

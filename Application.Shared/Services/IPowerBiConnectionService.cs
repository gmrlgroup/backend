using Application.Shared.Models;

namespace Application.Shared.Services;

/// <summary>Company-scoped CRUD for Power BI service-principal connections.</summary>
public interface IPowerBiConnectionService
{
    Task<List<PowerBiConnectionDto>> GetConnectionsAsync(string companyId);
    Task<PowerBiConnectionDto> CreateAsync(string companyId, PowerBiConnectionRequest request, string? createdBy);
    Task<PowerBiConnectionDto?> UpdateAsync(string connectionId, string companyId, PowerBiConnectionRequest request, string? modifiedBy);
    Task<bool> DeleteAsync(string connectionId, string companyId);

    /// <summary>Loads a connection with its secret decrypted in-memory for API calls. Never returned to the client.</summary>
    Task<PowerBiConnection?> GetForExecutionAsync(string connectionId, string companyId);
}

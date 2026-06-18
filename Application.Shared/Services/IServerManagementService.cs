using Application.Shared.Models;

namespace Application.Shared.Services;

public interface IServerManagementService
{
    /// <summary>Connects to the server (using the given or default credential) and lists its OS services.</summary>
    Task<List<RemoteServiceInfo>> DiscoverServicesAsync(string entityId, string? credentialId, string companyId, CancellationToken ct = default);

    Task<ServiceActionResult> StartServiceAsync(string entityId, string? credentialId, string companyId, string serviceName, CancellationToken ct = default);

    Task<ServiceActionResult> StopServiceAsync(string entityId, string? credentialId, string companyId, string serviceName, CancellationToken ct = default);
}

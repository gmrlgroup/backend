using Application.Shared.Enums;
using Application.Shared.Models;

namespace Application.Shared.Services;

/// <summary>
/// Performs remote service operations against a server. The management service selects an
/// executor via <see cref="Supports"/>. The passed <see cref="ServerCredential"/> carries a
/// DECRYPTED secret.
/// </summary>
public interface IRemoteServerExecutor
{
    bool Supports(ServerPlatform platform);

    Task<List<RemoteServiceInfo>> DiscoverServicesAsync(ServerCredential credential, string host, CancellationToken ct = default);

    Task<ServiceActionResult> StartServiceAsync(ServerCredential credential, string host, string serviceName, CancellationToken ct = default);

    Task<ServiceActionResult> StopServiceAsync(ServerCredential credential, string host, string serviceName, CancellationToken ct = default);
}

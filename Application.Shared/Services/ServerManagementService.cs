using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class ServerManagementService : IServerManagementService
{
    private readonly StatusDbContext _context;
    private readonly IServerCredentialService _credentialService;
    private readonly IEnumerable<IRemoteServerExecutor> _executors;

    public ServerManagementService(
        StatusDbContext context,
        IServerCredentialService credentialService,
        IEnumerable<IRemoteServerExecutor> executors)
    {
        _context = context;
        _credentialService = credentialService;
        _executors = executors;
    }

    public async Task<List<RemoteServiceInfo>> DiscoverServicesAsync(string entityId, string? credentialId, string companyId, CancellationToken ct = default)
    {
        var (credential, host, executor) = await ResolveAsync(entityId, credentialId, companyId);
        return await executor.DiscoverServicesAsync(credential, host, ct);
    }

    public async Task<ServiceActionResult> StartServiceAsync(string entityId, string? credentialId, string companyId, string serviceName, CancellationToken ct = default)
    {
        var (credential, host, executor) = await ResolveAsync(entityId, credentialId, companyId);
        return await executor.StartServiceAsync(credential, host, serviceName, ct);
    }

    public async Task<ServiceActionResult> StopServiceAsync(string entityId, string? credentialId, string companyId, string serviceName, CancellationToken ct = default)
    {
        var (credential, host, executor) = await ResolveAsync(entityId, credentialId, companyId);
        return await executor.StopServiceAsync(credential, host, serviceName, ct);
    }

    /// <summary>Loads the entity + decrypted credential, resolves the host, and picks the platform executor.</summary>
    private async Task<(ServerCredential credential, string host, IRemoteServerExecutor executor)> ResolveAsync(
        string entityId, string? credentialId, string companyId)
    {
        var entity = await _context.MonitoredAssets
            .FirstOrDefaultAsync(e => e.Id == entityId && e.CompanyId == companyId && !e.IsDeleted)
            ?? throw new InvalidOperationException("Server entity not found.");

        if (entity.EntityType != AssetType.Server)
            throw new InvalidOperationException("Service management is only available for Server entities.");

        var credential = await _credentialService.GetForExecutionAsync(entityId, credentialId, companyId)
            ?? throw new InvalidOperationException("No credential configured for this server.");

        var host = ResolveHost(credential, entity);
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("No host configured. Set a Host on the credential or a URL on the entity.");

        var executor = _executors.FirstOrDefault(x => x.Supports(credential.Platform))
            ?? throw new InvalidOperationException($"No executor available for platform '{credential.Platform}'.");

        return (credential, host, executor);
    }

    /// <summary>Prefers the credential Host, then the entity Url (scheme/path stripped), then the entity Name.</summary>
    private static string ResolveHost(ServerCredential credential, MonitoredAsset entity)
    {
        if (!string.IsNullOrWhiteSpace(credential.Host)) return credential.Host.Trim();

        if (!string.IsNullOrWhiteSpace(entity.Url))
        {
            var url = entity.Url.Trim();
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                return uri.Host;
            return url;
        }

        return entity.Name?.Trim() ?? string.Empty;
    }
}

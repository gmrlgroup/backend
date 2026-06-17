using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services;

public class ServerCredentialService : IServerCredentialService
{
    private readonly StatusDbContext _context;
    private readonly ICredentialProtector _protector;

    public ServerCredentialService(StatusDbContext context, ICredentialProtector protector)
    {
        _context = context;
        _protector = protector;
    }

    public async Task<List<ServerCredentialDto>> GetCredentialsAsync(string entityId, string companyId)
    {
        var credentials = await _context.ServerCredentials
            .Where(c => c.EntityId == entityId && c.CompanyId == companyId)
            .OrderByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return credentials.Select(ToDto).ToList();
    }

    public async Task<ServerCredentialDto> CreateAsync(string entityId, string companyId, ServerCredentialRequest request, string? createdBy)
    {
        var credential = new ServerCredential
        {
            Id = Guid.NewGuid().ToString(),
            EntityId = entityId,
            CompanyId = companyId,
            Name = request.Name,
            Platform = request.Platform,
            AuthType = request.AuthType,
            Host = request.Host,
            Port = request.Port > 0 ? request.Port : DefaultPort(request.Platform),
            Username = request.Username,
            SecretEncrypted = string.IsNullOrEmpty(request.Secret) ? null : _protector.Encrypt(request.Secret),
            IsDefault = request.IsDefault,
            CreatedBy = createdBy,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = DateTime.UtcNow
        };

        await EnsureSingleDefaultAsync(entityId, companyId, credential);

        _context.ServerCredentials.Add(credential);
        await _context.SaveChangesAsync();

        return ToDto(credential);
    }

    public async Task<ServerCredentialDto?> UpdateAsync(string credentialId, string entityId, string companyId, ServerCredentialRequest request, string? modifiedBy)
    {
        var credential = await _context.ServerCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.EntityId == entityId && c.CompanyId == companyId);
        if (credential == null) return null;

        credential.Name = request.Name;
        credential.Platform = request.Platform;
        credential.AuthType = request.AuthType;
        credential.Host = request.Host;
        credential.Port = request.Port > 0 ? request.Port : DefaultPort(request.Platform);
        credential.Username = request.Username;
        credential.IsDefault = request.IsDefault;
        credential.ModifiedBy = modifiedBy;
        credential.ModifiedOn = DateTime.UtcNow;

        // Only replace the secret when a new one is supplied; blank keeps the existing secret.
        if (!string.IsNullOrEmpty(request.Secret))
            credential.SecretEncrypted = _protector.Encrypt(request.Secret);

        await EnsureSingleDefaultAsync(entityId, companyId, credential);

        await _context.SaveChangesAsync();

        return ToDto(credential);
    }

    public async Task<bool> DeleteAsync(string credentialId, string entityId, string companyId)
    {
        var credential = await _context.ServerCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.EntityId == entityId && c.CompanyId == companyId);
        if (credential == null) return false;

        _context.ServerCredentials.Remove(credential);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ServerCredential?> GetForExecutionAsync(string entityId, string? credentialId, string companyId)
    {
        var query = _context.ServerCredentials
            .Where(c => c.EntityId == entityId && c.CompanyId == companyId);

        ServerCredential? credential = !string.IsNullOrEmpty(credentialId)
            ? await query.FirstOrDefaultAsync(c => c.Id == credentialId)
            : await query.OrderByDescending(c => c.IsDefault).ThenBy(c => c.Name).FirstOrDefaultAsync();

        if (credential == null) return null;

        // Decrypt the secret in-memory for execution; never persist or return this to the client.
        if (!string.IsNullOrEmpty(credential.SecretEncrypted))
            credential.SecretEncrypted = _protector.Decrypt(credential.SecretEncrypted);

        return credential;
    }

    /// <summary>When the given credential is the default, clears the flag on every other credential of the entity.</summary>
    private async Task EnsureSingleDefaultAsync(string entityId, string companyId, ServerCredential current)
    {
        if (!current.IsDefault) return;

        var others = await _context.ServerCredentials
            .Where(c => c.EntityId == entityId && c.CompanyId == companyId && c.Id != current.Id && c.IsDefault)
            .ToListAsync();

        foreach (var other in others)
            other.IsDefault = false;
    }

    private static int DefaultPort(ServerPlatform platform) => 22; // both Linux and Windows are managed over SSH

    private static ServerCredentialDto ToDto(ServerCredential c) => new()
    {
        Id = c.Id,
        EntityId = c.EntityId,
        Name = c.Name,
        Platform = c.Platform,
        AuthType = c.AuthType,
        Host = c.Host,
        Port = c.Port,
        Username = c.Username,
        IsDefault = c.IsDefault,
        HasSecret = !string.IsNullOrEmpty(c.SecretEncrypted),
        CreatedOn = c.CreatedOn,
        ModifiedOn = c.ModifiedOn
    };
}

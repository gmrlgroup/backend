using Application.Shared.Models;

namespace Application.Shared.Services;

public interface IServerCredentialService
{
    /// <summary>Lists credentials for an entity as masked DTOs (no secret).</summary>
    Task<List<ServerCredentialDto>> GetCredentialsAsync(string entityId, string companyId);

    Task<ServerCredentialDto> CreateAsync(string entityId, string companyId, ServerCredentialRequest request, string? createdBy);

    /// <summary>Updates a credential. A blank <see cref="ServerCredentialRequest.Secret"/> keeps the existing secret.</summary>
    Task<ServerCredentialDto?> UpdateAsync(string credentialId, string entityId, string companyId, ServerCredentialRequest request, string? modifiedBy);

    Task<bool> DeleteAsync(string credentialId, string entityId, string companyId);

    /// <summary>
    /// Returns a credential with its secret decrypted, for server-side execution only.
    /// When <paramref name="credentialId"/> is null the default (or first) credential is used.
    /// </summary>
    Task<ServerCredential?> GetForExecutionAsync(string entityId, string? credentialId, string companyId);
}

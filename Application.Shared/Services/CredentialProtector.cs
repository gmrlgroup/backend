using Microsoft.AspNetCore.DataProtection;

namespace Application.Shared.Services;

/// <summary>
/// Encrypts server/database credential secrets at rest using ASP.NET Core Data Protection.
/// Keys are persisted and DPAPI-protected by each host (see the web app and scheduler
/// Program.cs) so ciphertext written by one process can be read by the other on the same
/// machine. The protector purpose ("Application.ServerCredentials.v1") and the host's
/// SetApplicationName must match across processes for decryption to succeed.
/// </summary>
public class CredentialProtector : ICredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Application.ServerCredentials.v1");
    }

    public string Encrypt(string plaintext) => _protector.Protect(plaintext);

    public string Decrypt(string ciphertext) => _protector.Unprotect(ciphertext);
}

using Application.Shared.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Application.Services;

/// <summary>
/// Encrypts server credential secrets at rest using ASP.NET Core Data Protection.
/// Keys are persisted (see Program.cs) so ciphertext survives app restarts.
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

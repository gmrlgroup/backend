namespace Application.Shared.Services;

/// <summary>
/// Encrypts/decrypts server credential secrets at rest. Implemented in the web app
/// over ASP.NET Core Data Protection so the abstraction stays free of framework types.
/// </summary>
public interface ICredentialProtector
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

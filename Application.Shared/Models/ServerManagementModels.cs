using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>Safe, browser-facing view of a <see cref="ServerCredential"/> — never includes the secret.</summary>
public class ServerCredentialDto
{
    public string Id { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ServerPlatform Platform { get; set; }
    public CredentialAuthType AuthType { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public bool IsDefault { get; set; }
    /// <summary>True when a secret is stored, so the UI can show a masked placeholder.</summary>
    public bool HasSecret { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
}

/// <summary>Create/update payload sent from the client. <see cref="Secret"/> is plaintext and optional on update (blank keeps the existing secret).</summary>
public class ServerCredentialRequest
{
    public string Name { get; set; } = string.Empty;
    public ServerPlatform Platform { get; set; }
    public CredentialAuthType AuthType { get; set; } = CredentialAuthType.Password;
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Secret { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>A service discovered on a remote server.</summary>
public class RemoteServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    /// <summary>Normalized state — "running", "stopped", or the raw state when unknown.</summary>
    public string State { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
}

/// <summary>Result of a start/stop (or discovery) action against a remote server.</summary>
public class ServiceActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ServiceActionResult Ok(string message) => new() { Success = true, Message = message };
    public static ServiceActionResult Fail(string message) => new() { Success = false, Message = message };
}

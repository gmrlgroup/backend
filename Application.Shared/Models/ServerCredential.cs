using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// A credential used to remotely connect to a Server-type entity (MonitoredAsset)
/// to discover and start/stop its OS services. The secret (password or SSH private
/// key) is stored encrypted at rest in <see cref="SecretEncrypted"/> and is never
/// serialized to the browser — use <see cref="ServerCredentialDto"/> for that.
/// </summary>
[Table("server_credential")]
public class ServerCredential : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ServerPlatform Platform { get; set; }

    [Required]
    public CredentialAuthType AuthType { get; set; } = CredentialAuthType.Password;

    /// <summary>Host or IP to connect to. Falls back to the entity's Url/Name when empty.</summary>
    [MaxLength(500)]
    public string? Host { get; set; }

    public int Port { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    /// <summary>Encrypted secret (password or PEM private key). Never returned to the client.</summary>
    [JsonIgnore]
    public string? SecretEncrypted { get; set; }

    public bool IsDefault { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }
}

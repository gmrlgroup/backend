using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models;

/// <summary>
/// Elevated credential used for administrative work on a Database-type entity (creating users,
/// granting roles, reading space usage that the least-privilege account can't see).
/// <para>
/// Deliberately separate from <see cref="DatabaseConnection"/>: that one is used for the read-only
/// paths (table discovery, sampling, freshness) and is built with read-only intent everywhere. This
/// row only overrides <i>who</i> connects — host, port, catalog and SSL are still read from the
/// entity's <see cref="DatabaseConnection"/>, so there is one source of truth for where the server is.
/// </para>
/// The password is stored encrypted at rest in <see cref="SecretEncrypted"/> and is never serialized
/// to the browser — use <see cref="DatabaseAdminCredentialDto"/> for that. One row per entity.
/// </summary>
[Table("entity_database_admin_credential")]
public class DatabaseAdminCredential : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Username { get; set; }

    /// <summary>Encrypted password. Never returned to the client.</summary>
    [JsonIgnore]
    public string? SecretEncrypted { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }
}

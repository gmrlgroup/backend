using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

/// <summary>
/// Connection details used to enumerate the tables of a Database-type entity
/// (MonitoredAsset). The password is stored encrypted at rest in
/// <see cref="SecretEncrypted"/> and is never serialized to the browser — use
/// <see cref="DatabaseConnectionDto"/> for that. One row per entity.
/// </summary>
[Table("entity_database_connection")]
public class DatabaseConnection : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string EntityId { get; set; } = string.Empty;

    [Required]
    public DataSourceType DatabaseType { get; set; }

    /// <summary>Host or IP of the database server. Unused for DuckDB (see <see cref="FilePath"/>).</summary>
    [MaxLength(500)]
    public string? Host { get; set; }

    public int Port { get; set; }

    /// <summary>The database/catalog name to connect to.</summary>
    [MaxLength(200)]
    public string? DatabaseName { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    /// <summary>Encrypted password. Never returned to the client.</summary>
    [JsonIgnore]
    public string? SecretEncrypted { get; set; }

    public bool UseSsl { get; set; }

    /// <summary>Local file path for a DuckDB database.</summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(EntityId))]
    public virtual MonitoredAsset? Entity { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models;

/// <summary>
/// An Azure AD service-principal connection used to call the Power BI REST API.
/// Company-scoped (via <see cref="BaseModel.CompanyId"/>) and reusable across datasets — a single
/// company can have several, one per managed Power BI tenant. The client secret is stored encrypted
/// at rest in <see cref="ClientSecretEncrypted"/> and is never serialized to the browser — use
/// <see cref="PowerBiConnectionDto"/> for that.
/// </summary>
[Table("power_bi_connection")]
public class PowerBiConnection : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Azure AD tenant (directory) id the service principal lives in.</summary>
    [Required]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Service-principal application (client) id.</summary>
    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Encrypted client secret. Never returned to the client.</summary>
    [JsonIgnore]
    public string? ClientSecretEncrypted { get; set; }

    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public virtual ICollection<PowerBiDatasetLink>? DatasetLinks { get; set; } = new List<PowerBiDatasetLink>();
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models.Data;

/// <summary>
/// A long-lived API key that lets an external (non-interactive) caller reach the data API.
/// The raw secret is shown to the creator exactly once; only its hash is stored here.
/// Access is granted through <see cref="Scopes"/> (per dataset/table, read and/or import).
/// </summary>
public class ApiKey
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string CompanyId { get; set; } = string.Empty;

    [Required(ErrorMessage = "A name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    // SHA-256 hash (base64) of the raw key. The raw key itself is never persisted.
    [JsonIgnore]
    public string KeyHash { get; set; } = string.Empty;

    // Non-secret leading fragment (e.g. "fb_a1b2c3d4") so a key stays recognizable in lists.
    public string KeyPrefix { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public List<ApiKeyScope> Scopes { get; set; } = new();

    /// <summary>True when the key is neither revoked nor past its expiry.</summary>
    [JsonIgnore]
    [NotMapped]
    public bool IsActive => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Application.Shared.Enums;

namespace Application.Shared.Models;

[Table("incident_update")]
public class IncidentUpdate : BaseModel
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string IncidentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public IncidentStatus? StatusChange { get; set; }

    [MaxLength(200)]
    public string? Author { get; set; }

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public virtual Incident? Incident { get; set; } = null!;
}

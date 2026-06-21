using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Shared.Models.Data;

/// <summary>
/// One access grant for an <see cref="ApiKey"/>: a dataset (optionally narrowed to a single table)
/// plus what the key may do there. A null <see cref="TableName"/> means every table in the dataset.
/// </summary>
public class ApiKeyScope
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string ApiKeyId { get; set; } = string.Empty;

    public string DatasetId { get; set; } = string.Empty;

    // Null = all tables in the dataset; otherwise the specific table this grant covers.
    public string? TableName { get; set; }

    public bool CanRead { get; set; } = true;
    public bool CanImport { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    [ForeignKey(nameof(ApiKeyId))]
    public ApiKey? ApiKey { get; set; }
}

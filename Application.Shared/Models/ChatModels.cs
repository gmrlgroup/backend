using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Application.Shared.Models.User;

namespace Application.Shared.Models;

public class ChatMessage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string CompanyId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(5000)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public ChatMessageType Type { get; set; }

    public string? ReferencedDatasetIds { get; set; } // JSON array of dataset IDs

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? SessionId { get; set; }

    // Navigation properties
    public Company? Company { get; set; }
    public ApplicationUser? User { get; set; }
}

public enum ChatMessageType
{
    User = 0,
    Assistant = 1,
    System = 2
}

public class ChatRequest
{
    [Required]
    [StringLength(5000)]
    public string Message { get; set; } = string.Empty;

    public List<string> DatasetIds { get; set; } = new();
    public List<TableReference> TableReferences { get; set; } = new();

    public string? SessionId { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<DatasetReference> ReferencedDatasets { get; set; } = new();
    public List<TableReference> ReferencedTables { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DatasetReference
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class DatasetSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
}

public class TableReference
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TableColumn> Columns { get; set; } = new();
    public List<Dictionary<string, object>> SampleData { get; set; } = new();
}

public class TableColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
}

public class TableSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public List<TableColumn> Columns { get; set; } = new();
}

/// <summary>
/// Configuration for Azure OpenAI service integration
/// </summary>
public class AzureOpenAIConfiguration
{
    /// <summary>
    /// Azure OpenAI endpoint URL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the deployed model
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;
    
    /// <summary>
    /// API version to use
    /// </summary>
    public string ApiVersion { get; set; } = "2024-08-01-preview";
    
    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    public int MaxTokens { get; set; } = 1000;
    
    /// <summary>
    /// Temperature for response generation (0.0 to 1.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;
    
    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum retry attempts for failed requests
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}

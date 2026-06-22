using System;
using System.Collections.Generic;

namespace Application.Shared.Models.Data;

/// <summary>
/// Kind-specific connection details for an ingestion source, serialized into
/// <see cref="IngestionSource.SourceConfig"/>. Secrets are NOT stored here — they live encrypted
/// in <see cref="IngestionSource.SecretEncrypted"/>.
/// </summary>
public class IngestionSourceConfig
{
    // --- ExternalDatabase ---
    // Either an explicit SELECT (Query), or Schema + Table for a plain "SELECT * FROM schema.table".
    public string? Query { get; set; }
    public string? Schema { get; set; }
    public string? Table { get; set; }

    // --- Rest ---
    public string? Url { get; set; }
    public string? Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    // Dotted path to the rows array in the JSON response (e.g. "data.items"); empty = whole document.
    public string? JsonPath { get; set; }
    // none | basic | bearer — the secret carries the password/token.
    public string? AuthType { get; set; }
    public string? Username { get; set; }

    // --- Blob ---
    public string? Container { get; set; }
    public string? BlobPath { get; set; }

    // --- Sftp ---
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? SftpUsername { get; set; }
    public string? RemotePath { get; set; }

    // File format of the fetched object (Blob/SFTP). REST is always JSON; DB pulls produce CSV.
    public string? FileFormat { get; set; }
}

/// <summary>Client-facing view of an ingestion source — never includes the raw secret.</summary>
public class IngestionSourceDto
{
    public string Id { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string? SourceEntityId { get; set; }
    public IngestionSourceConfig Config { get; set; } = new();
    public bool HasSecret { get; set; }
    public string ImportMode { get; set; } = "Append";
    public List<string> KeyColumns { get; set; } = new();
    public bool CreateIfNotExists { get; set; } = true;
    public string? IncrementalColumn { get; set; }
    public string? IncrementalLastValue { get; set; }
    public string CronExpression { get; set; } = "0 * * * *";
    public string? TimeZone { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunMessage { get; set; }
    public int? LastRunRows { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Create/update payload for an ingestion source. Secret is write-only (plaintext in, encrypted at rest).</summary>
public class SaveIngestionSourceRequest
{
    public string TargetTable { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceKind { get; set; } = "ExternalDatabase";
    public string? SourceEntityId { get; set; }
    public IngestionSourceConfig Config { get; set; } = new();
    // When non-null, replaces the stored secret; null leaves it unchanged.
    public string? Secret { get; set; }
    public string ImportMode { get; set; } = "Append";
    public List<string> KeyColumns { get; set; } = new();
    public bool CreateIfNotExists { get; set; } = true;
    public string? IncrementalColumn { get; set; }
    public string CronExpression { get; set; } = "0 * * * *";
    public string? TimeZone { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>Client-facing view of a single ingestion run (history).</summary>
public class IngestionRunDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? RowsIngested { get; set; }
    public string? ErrorMessage { get; set; }
}

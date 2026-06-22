namespace Application.Shared.Models.Data;

/// <summary>
/// Request to infer column data types from a sample of the uploaded data. Sent from the import
/// "Configure Schema" step: the column names, the types currently pre-selected (by the name-based
/// heuristic), and the first few data rows so the AI can correct the types based on real values.
/// </summary>
public class SchemaInferenceRequest
{
    /// <summary>Column names, in order.</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>Currently pre-selected data types, aligned by index to <see cref="Columns"/>. Optional.</summary>
    public List<string> CurrentTypes { get; set; } = new();

    /// <summary>Sample data rows (e.g. the first 10). Each row's cells are aligned by index to <see cref="Columns"/>.</summary>
    public List<List<string?>> SampleRows { get; set; } = new();
}

public class ColumnTypeSuggestion
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Suggested type, guaranteed to be one of <see cref="Column.CommonDataTypes"/>.</summary>
    public string DataType { get; set; } = string.Empty;
}

public class SchemaInferenceResult
{
    public List<ColumnTypeSuggestion> Columns { get; set; } = new();

    /// <summary>Non-null when the suggestion could not be produced (e.g. AI unavailable); the caller keeps the existing types.</summary>
    public string? Error { get; set; }
}

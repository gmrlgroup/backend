using System.Text;
using System.Text.Json;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Application.Shared.Services.Data;

public interface ISchemaInferenceService
{
    /// <summary>
    /// Asks Azure OpenAI (Azure AI Foundry) to choose the best data type for each column from
    /// <see cref="Column.CommonDataTypes"/>, given the column names, the currently pre-selected
    /// types, and a sample of the data. Never throws — failures are returned via
    /// <see cref="SchemaInferenceResult.Error"/> so the caller can keep the existing types.
    /// </summary>
    Task<SchemaInferenceResult> InferColumnTypesAsync(SchemaInferenceRequest request, CancellationToken cancellationToken = default);
}

public class SchemaInferenceService : ISchemaInferenceService
{
    // Cap how much we send so a wide/long file can't blow up the prompt (and cost).
    private const int MaxSampleRows = 10;
    private const int MaxCellLength = 120;

    private readonly AzureOpenAIConfiguration _config;
    private readonly ILogger<SchemaInferenceService> _logger;
    private readonly AzureOpenAIClient _client;

    public SchemaInferenceService(IOptions<AzureOpenAIConfiguration> config, ILogger<SchemaInferenceService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _client = new AzureOpenAIClient(new Uri(_config.Endpoint), new AzureKeyCredential(_config.ApiKey));
    }

    public async Task<SchemaInferenceResult> InferColumnTypesAsync(SchemaInferenceRequest request, CancellationToken cancellationToken = default)
    {
        var result = new SchemaInferenceResult();

        if (request?.Columns == null || request.Columns.Count == 0)
        {
            result.Error = "No columns provided.";
            return result;
        }

        var allowedTypes = Column.CommonDataTypes;

        try
        {
            var chatClient = _client.GetChatClient(_config.DeploymentName);
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(BuildSystemPrompt(allowedTypes)),
                new UserChatMessage(BuildUserPrompt(request, allowedTypes)),
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0f,
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            var response = await chatClient.CompleteChatAsync(messages, options, cts.Token);
            var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                result.Error = "The AI service returned an empty response.";
                return result;
            }

            result.Columns = ParseSuggestions(content, request.Columns, allowedTypes);
            if (result.Columns.Count == 0)
                result.Error = "The AI service did not return any usable type suggestions.";

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Error = "The AI request timed out. Please try again.";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema inference failed.");
            // Surface the underlying reason (endpoint, auth, deployment, JSON-mode support, etc.) — this
            // endpoint is admin-gated, so the detail is useful here rather than a generic message.
            result.Error = $"AI service error: {ex.GetType().Name}: {ex.Message}";
            return result;
        }
    }

    private static string BuildSystemPrompt(IReadOnlyCollection<string> allowedTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a data engineering assistant that determines the most appropriate column data type for a table being created from uploaded tabular (CSV) data.");
        sb.AppendLine("You are given the column names, the type currently pre-selected by a simple name-based heuristic, and a small sample of real data rows.");
        sb.AppendLine("Choose the best type for EACH column based primarily on the sample values, using the column name only as a secondary hint.");
        sb.AppendLine();
        sb.AppendLine("You MUST choose each data type from EXACTLY this allowed set (use the value verbatim, uppercase):");
        sb.AppendLine(string.Join(", ", allowedTypes));
        sb.AppendLine();
        sb.AppendLine("Guidance:");
        sb.AppendLine("- Whole numbers -> INTEGER, or BIGINT if values can exceed ~2,000,000,000.");
        sb.AppendLine("- Numbers with decimals representing money/quantities -> DECIMAL; general floating point -> DOUBLE.");
        sb.AppendLine("- true/false, yes/no, 0/1 flags -> BOOLEAN.");
        sb.AppendLine("- Calendar dates only -> DATE; date+time -> TIMESTAMP; time of day only -> TIME.");
        sb.AppendLine("- UUID/GUID-formatted identifiers -> UUID.");
        sb.AppendLine("- JSON objects/arrays -> JSON.");
        sb.AppendLine("- Short free text or codes -> VARCHAR; long free text -> TEXT.");
        sb.AppendLine("- If the sample is empty or ambiguous, keep the current type if it is reasonable, otherwise use VARCHAR.");
        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object ONLY, in this exact shape:");
        sb.AppendLine("{ \"columns\": [ { \"name\": \"<column name>\", \"data_type\": \"<one of the allowed types>\" } ] }");
        sb.AppendLine("Include every column exactly once. Do not add commentary.");
        return sb.ToString();
    }

    private static string BuildUserPrompt(SchemaInferenceRequest request, IReadOnlyCollection<string> allowedTypes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Columns (name and currently pre-selected type):");
        for (int i = 0; i < request.Columns.Count; i++)
        {
            var current = i < request.CurrentTypes.Count ? request.CurrentTypes[i] : "VARCHAR";
            sb.AppendLine($"- {request.Columns[i]} (current: {current})");
        }

        sb.AppendLine();
        sb.AppendLine($"Sample rows (up to {MaxSampleRows}), values aligned to the column order above:");

        var rows = request.SampleRows ?? new List<List<string?>>();
        if (rows.Count == 0)
        {
            sb.AppendLine("(no sample rows available)");
        }
        else
        {
            foreach (var row in rows.Take(MaxSampleRows))
            {
                var cells = new List<string>();
                for (int i = 0; i < request.Columns.Count; i++)
                {
                    var value = row != null && i < row.Count ? row[i] : null;
                    cells.Add(Clip(value));
                }
                // JSON-array form keeps cell boundaries unambiguous even when values contain commas.
                sb.AppendLine(JsonSerializer.Serialize(cells));
            }
        }

        return sb.ToString();
    }

    private static string Clip(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var v = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return v.Length > MaxCellLength ? v.Substring(0, MaxCellLength) : v;
    }

    private List<ColumnTypeSuggestion> ParseSuggestions(string content, List<string> requestedColumns, IReadOnlyCollection<string> allowedTypes)
    {
        var suggestions = new List<ColumnTypeSuggestion>();

        // Map allowed types case-insensitively back to their canonical (uppercase) form.
        var canonical = allowedTypes.ToDictionary(t => t, t => t, StringComparer.OrdinalIgnoreCase);
        var requested = new HashSet<string>(requestedColumns, StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(StripCodeFences(content));
            if (!doc.RootElement.TryGetProperty("columns", out var columnsEl) || columnsEl.ValueKind != JsonValueKind.Array)
                return suggestions;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in columnsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                var type = item.TryGetProperty("data_type", out var typeEl) ? typeEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type)) continue;

                // Only trust columns we actually asked about, and types from the allowed set.
                if (!requested.Contains(name)) continue;
                if (!canonical.TryGetValue(type.Trim(), out var canonicalType)) continue;
                if (!seen.Add(name)) continue;

                suggestions.Add(new ColumnTypeSuggestion { Name = name, DataType = canonicalType });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse schema inference response: {Content}", content);
        }

        return suggestions;
    }

    private static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        // Drop the opening fence (``` or ```json) and the closing fence.
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0) trimmed = trimmed.Substring(firstNewline + 1);
        if (trimmed.EndsWith("```")) trimmed = trimmed.Substring(0, trimmed.Length - 3);
        return trimmed.Trim();
    }
}

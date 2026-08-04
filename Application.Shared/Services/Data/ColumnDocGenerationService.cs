using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Application.Shared.Services;
using Application.Shared.Services.Logging;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Application.Shared.Services.Data;

public class ColumnDocGenerationResult
{
    public int ColumnsDocumented { get; set; }

    /// <summary>The table's live column count — the denominator for <see cref="ColumnsDocumented"/>.</summary>
    public int ColumnsTotal { get; set; }

    /// <summary>
    /// Set when the run succeeded but didn't cover every column — a batch failed, or a very wide table ran
    /// out of the request's time budget. This is still a success (the columns that were generated are saved);
    /// the message tells the user what's left and that running it again continues from there.
    /// </summary>
    public string? Note { get; set; }

    public string? Error { get; set; }
}

public interface IColumnDocGenerationService
{
    /// <summary>
    /// Samples a table's columns + a few sanitized rows and asks Azure OpenAI to propose a description,
    /// display name, semantic type/unit and a PII flag per column, then persists them as AI-generated
    /// docs. Columns are documented in batches (a few AI calls in parallel), each persisted as it lands,
    /// so a wide table neither blows the request timeout nor loses the work it already paid for. When
    /// <paramref name="snapshotMode"/> is true the table is read from the dataset's DuckDB snapshot; when
    /// false it is read from the External dataset's live source. Never throws — a total failure comes back
    /// via <see cref="ColumnDocGenerationResult.Error"/> and a partial one via
    /// <see cref="ColumnDocGenerationResult.Note"/>.
    /// </summary>
    Task<ColumnDocGenerationResult> GenerateAsync(string companyId, string datasetId, string tableName, bool snapshotMode, CancellationToken ct = default);
}

public class ColumnDocGenerationService : IColumnDocGenerationService
{
    // Cap what we send so a wide/long table can't blow up the prompt (and cost) — and to limit PII exposure.
    private const int MaxSampleRows = 10;
    private const int MaxCellLength = 120;

    // Wide tables are documented in batches — one chat completion per group of columns. One call for the
    // whole table has to emit JSON for every column, and that output is produced token by token, so a few
    // hundred columns reliably ran past the timeout: that was the "AI request timed out" 400. A batch of
    // this size answers in seconds, several run at once, and each is saved the moment it lands.
    private const int ColumnsPerBatch = 25;

    // Deliberately low: every batch hits the same Azure OpenAI deployment against one shared per-minute
    // token quota, so more parallelism mostly buys 429s that the SDK then has to retry away.
    private const int MaxParallelBatches = 4;

    // Per-batch ceiling — generous for 25 columns. A batch slower than this is stuck, and waiting on it
    // only spends the budget the remaining batches need.
    private const int BatchTimeoutSeconds = 45;

    // Whole-run ceiling. It has to stay under the Blazor client's 100s HttpClient timeout: past that the
    // browser aborts first and the user sees a connection error instead of the partial-progress result.
    private const int OverallBudgetSeconds = 85;

    private readonly AzureOpenAIConfiguration _config;
    private readonly AzureOpenAIClient _client;
    private readonly IDuckdbService _duckdb;
    private readonly IDatabaseTableService _dbTables;
    private readonly ApplicationDbContext _db;
    private readonly IDatasetDocService _docService;
    private readonly ILogger<ColumnDocGenerationService> _logger;
    private readonly IDebugLogService _debug;

    public ColumnDocGenerationService(
        IOptions<AzureOpenAIConfiguration> config,
        IDuckdbService duckdb,
        IDatabaseTableService dbTables,
        ApplicationDbContext db,
        IDatasetDocService docService,
        ILogger<ColumnDocGenerationService> logger,
        IDebugLogService debug)
    {
        _config = config.Value;
        _client = new AzureOpenAIClient(new Uri(_config.Endpoint), new AzureKeyCredential(_config.ApiKey));
        _duckdb = duckdb;
        _dbTables = dbTables;
        _db = db;
        _docService = docService;
        _logger = logger;
        _debug = debug;
    }

    public async Task<ColumnDocGenerationResult> GenerateAsync(string companyId, string datasetId, string tableName, bool snapshotMode, CancellationToken ct = default)
    {
        var result = new ColumnDocGenerationResult();

        List<Column> columns;
        try
        {
            columns = await _docService.GetLiveColumnsAsync(companyId, datasetId, tableName, snapshotMode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ColumnDoc] Failed to read columns for {Dataset}/{Table}.", datasetId, tableName);
            await _debug.LogAsync(companyId, DebugLevel.Error, "DataDocs",
                $"Couldn't read columns for '{tableName}': {ex.Message}",
                datasetId: datasetId, tableName: tableName, error: ex.Message, ct: ct);
            result.Error = "Couldn't read the table's columns.";
            return result;
        }

        if (columns.Count == 0)
        {
            await _debug.LogAsync(companyId, DebugLevel.Warn, "DataDocs",
                $"Nothing to document for '{tableName}': the table has no columns.",
                datasetId: datasetId, tableName: tableName, ct: ct);
            result.Error = "This table has no columns to document.";
            return result;
        }

        result.ColumnsTotal = columns.Count;

        await _debug.LogAsync(companyId, DebugLevel.Debug, "DataDocs",
            $"Documenting '{tableName}': {columns.Count} column(s), snapshot={snapshotMode}.",
            datasetId: datasetId, tableName: tableName,
            context: new { columns = columns.Count, snapshotMode }, ct: ct);

        var sampleRows = await FetchSampleRowsAsync(companyId, datasetId, tableName, snapshotMode, ct);

        await _debug.LogAsync(companyId, DebugLevel.Debug, "DataDocs",
            $"Sample fetch for '{tableName}': {sampleRows.Count} row(s) collected.",
            datasetId: datasetId, tableName: tableName,
            context: new { sampleRows = sampleRows.Count }, ct: ct);

        var batches = BuildBatches(await LoadDocumentedColumnNamesAsync(companyId, datasetId, tableName, snapshotMode, ct), columns);

        // Hard ceiling for the whole run, on top of each batch's own timeout. Linked to the caller's token
        // so a browser that goes away still stops the work.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(OverallBudgetSeconds));

        // The ApplicationDbContext behind _docService and _debug is scoped and not thread-safe, so only the
        // AI calls actually run concurrently — every database touch is serialized through this gate.
        using var dbGate = new SemaphoreSlim(1, 1);
        using var slots = new SemaphoreSlim(MaxParallelBatches, MaxParallelBatches);

        var documented = 0;
        var failedBatches = 0;
        var unfinishedBatches = 0;
        string? firstError = null;

        // Never throws: each batch records its own outcome so one bad batch can't discard the others' work.
        async Task RunBatchAsync(List<Column> batch)
        {
            try
            {
                await slots.WaitAsync(budget.Token);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref unfinishedBatches);
                return;
            }

            try
            {
                // The budget may have expired while this batch sat in the queue — don't start work that
                // can't finish, just report it as unfinished so the caller can say "run it again".
                if (budget.IsCancellationRequested)
                {
                    Interlocked.Increment(ref unfinishedBatches);
                    return;
                }

                var docs = await GenerateBatchAsync(tableName, batch, sampleRows, budget.Token);
                if (docs.Count == 0)
                {
                    Interlocked.Increment(ref failedBatches);
                    Interlocked.CompareExchange(ref firstError, "the AI service returned no usable column documentation", null);
                    return;
                }

                // Persist per batch rather than once at the end: a run that later runs out of budget still
                // leaves these columns documented. CancellationToken.None on purpose — the tokens for this
                // batch are already spent, so the save completes even when the budget or the client is gone.
                await dbGate.WaitAsync(CancellationToken.None);
                try
                {
                    await _docService.ApplyGeneratedDocsAsync(companyId, datasetId, tableName, snapshotMode, docs, CancellationToken.None);
                }
                finally
                {
                    dbGate.Release();
                }

                Interlocked.Add(ref documented, docs.Count);
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref unfinishedBatches);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failedBatches);
                Interlocked.CompareExchange(ref firstError, $"{ex.GetType().Name}: {ex.Message}", null);
                _logger.LogError(ex, "[ColumnDoc] Batch failed for {Dataset}/{Table}.", datasetId, tableName);
            }
            finally
            {
                slots.Release();
            }
        }

        var sw = Stopwatch.StartNew();
        await Task.WhenAll(batches.Select(RunBatchAsync));
        sw.Stop();

        result.ColumnsDocumented = documented;

        await _debug.LogAsync(companyId, DebugLevel.Info, "DataDocs",
            $"Azure OpenAI ({_config.DeploymentName}) documented {documented}/{columns.Count} column(s) of '{tableName}' " +
            $"in {batches.Count} batch(es) / {sw.ElapsedMilliseconds} ms ({failedBatches} failed, {unfinishedBatches} unfinished).",
            datasetId: datasetId, tableName: tableName, durationMs: sw.ElapsedMilliseconds,
            context: new
            {
                deployment = _config.DeploymentName,
                batches = batches.Count,
                documented,
                columns = columns.Count,
                failedBatches,
                unfinishedBatches
            }, ct: ct);

        if (documented == 0)
        {
            result.Error = unfinishedBatches > 0 && failedBatches == 0
                ? $"The AI request ran out of time ({OverallBudgetSeconds}s) before any column could be documented. Please try again."
                : $"AI service error: {firstError ?? "the AI service didn't return any usable column documentation."}";
            return result;
        }

        if (documented < columns.Count)
        {
            var missing = columns.Count - documented;
            result.Note = unfinishedBatches > 0
                ? $"Documented {documented} of {columns.Count} columns before the {OverallBudgetSeconds}s limit. " +
                  $"Run Generate again to continue with the remaining {missing}."
                : $"Documented {documented} of {columns.Count} columns; {missing} couldn't be generated " +
                  $"({firstError ?? "no reason reported"}). Run Generate again to retry them.";
        }

        return result;
    }

    /// <summary>
    /// Groups the columns into per-request batches, putting anything still undocumented first.
    /// <see cref="Enumerable.OrderBy{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey})"/> is stable, so
    /// a table that has never been documented keeps its natural column order — which matters, because
    /// neighbouring columns are usually related and give the model context. Only a re-run after a partial
    /// one reorders anything, and that's exactly when you want the missing columns to go first: it's what
    /// makes "run Generate again" finish a table too wide for a single request.
    /// </summary>
    private static List<List<Column>> BuildBatches(HashSet<string> alreadyDocumented, List<Column> columns)
        => columns
            .OrderBy(c => alreadyDocumented.Contains(c.Name) ? 1 : 0)
            .Chunk(ColumnsPerBatch)
            .Select(b => b.ToList())
            .ToList();

    // Which columns already have a saved doc. Only used to order the work, so a failure here costs nothing
    // beyond that ordering.
    private async Task<HashSet<string>> LoadDocumentedColumnNamesAsync(string companyId, string datasetId, string tableName, bool snapshotMode, CancellationToken ct)
    {
        try
        {
            var saved = await _docService.GetSavedColumnDocsAsync(companyId, datasetId, tableName, snapshotMode, ct);
            return new HashSet<string>(saved.Keys, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ColumnDoc] Couldn't read existing docs for {Dataset}/{Table}; batching in natural order.", datasetId, tableName);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// One chat completion for one group of columns. Returns the parsed docs, or an empty list when the
    /// model gave us nothing usable. Cancellation and transport failures bubble up to the caller's
    /// per-batch handling.
    /// </summary>
    private async Task<List<SaveColumnDocRequest>> GenerateBatchAsync(
        string tableName, List<Column> batch, List<Dictionary<string, string?>> sampleRows, CancellationToken ct)
    {
        var chatClient = _client.GetChatClient(_config.DeploymentName);
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(BuildSystemPrompt()),
            new UserChatMessage(BuildUserPrompt(tableName, batch, sampleRows)),
        };
        var options = new ChatCompletionOptions
        {
            Temperature = 0f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(_config.TimeoutSeconds, BatchTimeoutSeconds)));

        var response = await chatClient.CompleteChatAsync(messages, options, cts.Token);
        var content = response.Value.Content.Count > 0 ? response.Value.Content[0].Text : null;
        return string.IsNullOrWhiteSpace(content)
            ? new List<SaveColumnDocRequest>()
            : ParseDocs(content, batch);
    }

    /// <summary>
    /// A small, read-only sample so the model has real values to reason about. Failure is non-fatal — we can
    /// still document from names + types. In snapshot mode this reads the dataset's DuckDB copy; in source
    /// mode the External dataset's live source table. Rows come back keyed by column name so each batch can
    /// pull just its own cells instead of carrying the whole wide table's values in every prompt.
    /// </summary>
    private async Task<List<Dictionary<string, string?>>> FetchSampleRowsAsync(
        string companyId, string datasetId, string tableName, bool snapshotMode, CancellationToken ct)
    {
        var empty = new List<Dictionary<string, string?>>();
        try
        {
            SqlQueryResult? sample = null;
            if (snapshotMode)
            {
                var quoted = "\"" + tableName.Replace("\"", "\"\"") + "\"";
                sample = await _duckdb.ExecuteSqlAsync(datasetId, $"SELECT * FROM {quoted} LIMIT {MaxSampleRows}", allowWrite: false, maxRows: MaxSampleRows, ct);
            }
            else
            {
                var dataset = await _db.Dataset.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == datasetId && d.CompanyId == companyId, ct);
                if (dataset?.SourceType == DatasetSourceType.External && !string.IsNullOrWhiteSpace(dataset.SourceEntityId))
                    // Server-side TOP/LIMIT so a heavy source view isn't fully evaluated just for a few rows.
                    sample = await _dbTables.GetTableSampleAsync(dataset.SourceEntityId!, companyId, tableName, MaxSampleRows, ct);
            }

            if (sample == null || sample.Error != null) return empty;

            var rows = new List<Dictionary<string, string?>>(sample.Rows.Count);
            foreach (var row in sample.Rows)
            {
                var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                // Indexer rather than Add: a result set can repeat a name (or differ only by case), and a
                // duplicate-key throw would cost us the entire sample over one cell.
                foreach (var kv in row) cells[kv.Key] = kv.Value?.ToString();
                rows.Add(cells);
            }
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ColumnDoc] Sample fetch failed for {Dataset}/{Table}; continuing without samples.", datasetId, tableName);
            await _debug.LogAsync(companyId, DebugLevel.Warn, "DataDocs",
                $"Sample fetch failed for '{tableName}'; continuing without samples: {ex.Message}",
                datasetId: datasetId, tableName: tableName, error: ex.Message, ct: ct);
            return empty;
        }
    }

    private static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a data-catalog assistant. Given a database table's column names, types and a small sample of rows,");
        sb.AppendLine("you document each column so analysts and query-writing tools understand it.");
        sb.AppendLine();
        sb.AppendLine("For EACH column provide:");
        sb.AppendLine("- displayName: a short, human-friendly label (Title Case), e.g. \"Net Amount\" for net_amt_acy.");
        sb.AppendLine("- description: one concise sentence explaining what the column holds. Use the sample values as evidence; don't invent facts.");
        sb.AppendLine("- semanticType: one of currency, percentage, date, datetime, time, email, phone, name, address, url, identifier, quantity, count, category, boolean, text, other.");
        sb.AppendLine("- unit: a unit when clearly applicable (e.g. \"USD\", \"kg\", \"%\", \"days\"), otherwise null.");
        sb.AppendLine("- isPii: true if the column likely contains personal data (names, emails, phone numbers, addresses, national ids). This is an advisory hint for human review.");
        sb.AppendLine("- piiType: when isPii is true, a short label like \"email\", \"name\", \"phone\", \"address\"; otherwise null.");
        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object ONLY, in this exact shape:");
        sb.AppendLine("{ \"columns\": [ { \"name\": \"<column name>\", \"displayName\": \"...\", \"description\": \"...\", \"semanticType\": \"...\", \"unit\": null, \"isPii\": false, \"piiType\": null } ] }");
        sb.AppendLine("Use the column name EXACTLY as given. Include every column once. Do not add commentary outside the JSON.");
        return sb.ToString();
    }

    private static string BuildUserPrompt(string tableName, List<Column> columns, List<Dictionary<string, string?>> sampleRows)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Table: {tableName}");
        sb.AppendLine("Columns (name and type):");
        foreach (var c in columns)
            sb.AppendLine($"- {c.Name} ({c.DataType})");

        sb.AppendLine();
        sb.AppendLine($"Sample rows (up to {MaxSampleRows}), values aligned to the column order above:");
        if (sampleRows.Count == 0)
        {
            sb.AppendLine("(no sample rows available)");
        }
        else
        {
            foreach (var row in sampleRows.Take(MaxSampleRows))
            {
                // Only the columns in this batch: the sample is keyed by name, so one batch of a wide table
                // never drags the rest of its cells into the prompt.
                var cells = columns.Select(c => Clip(row.TryGetValue(c.Name, out var v) ? v : null)).ToList();
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

    private List<SaveColumnDocRequest> ParseDocs(string content, List<Column> columns)
    {
        var docs = new List<SaveColumnDocRequest>();
        var known = new HashSet<string>(columns.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(StripCodeFences(content));
            if (!doc.RootElement.TryGetProperty("columns", out var columnsEl) || columnsEl.ValueKind != JsonValueKind.Array)
                return docs;

            foreach (var item in columnsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var name = GetString(item, "name");
                if (string.IsNullOrWhiteSpace(name) || !known.Contains(name) || !seen.Add(name)) continue;

                docs.Add(new SaveColumnDocRequest
                {
                    ColumnName = name,
                    DisplayName = GetString(item, "displayName"),
                    Description = GetString(item, "description"),
                    SemanticType = GetString(item, "semanticType"),
                    Unit = GetString(item, "unit"),
                    IsPii = GetBool(item, "isPii"),
                    PiiType = GetString(item, "piiType"),
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[ColumnDoc] Could not parse generation response: {Content}", content);
        }
        return docs;
    }

    private static string? GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b));

    private static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline >= 0) trimmed = trimmed.Substring(firstNewline + 1);
        if (trimmed.EndsWith("```")) trimmed = trimmed.Substring(0, trimmed.Length - 3);
        return trimmed.Trim();
    }
}

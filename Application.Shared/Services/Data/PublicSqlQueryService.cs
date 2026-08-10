using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Shared.Data;
using Application.Shared.Enums;
using Application.Shared.Models;
using Application.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Shared.Services.Data;

/// <summary>
/// Runs read-only SQL for the API-key public API, on behalf of an acting user, with that user's table
/// grants, column grants and row-level security applied.
/// </summary>
/// <remarks>
/// <b>This service is the enforcement point for column masking and row-level security.</b> Until it
/// existed, the header comment on <see cref="UserRlsFilter"/> was accurate: this backend served the
/// grants and left it to "the consuming query engine" to apply them. The consumer that prompted this
/// endpoint (Relay's dataset agent) cannot apply them — the RLS records carry no table name, so a
/// consumer has no way to know which table a filter belongs to — and, more fundamentally, a consumer is
/// the wrong place for it: <c>X-User-Id</c> is caller-asserted, so only the side holding the grants can
/// decide anything.
/// <para>
/// Consequently nothing here may assume a caller validated anything. The SQL is re-checked, the grants
/// are re-read, and the query is rewritten to run against secured relations
/// (<see cref="SecuredSqlBuilder"/>) regardless of what the caller claims to have done.
/// </para>
/// <para>
/// Every branch that cannot produce a provably-secured statement <b>fails closed</b> and runs nothing.
/// The failure modes are reported with HTTP 200 and an <see cref="PublicSqlErrorCodes"/> code, matching
/// how <see cref="SqlQueryResult"/> has always reported SQL problems — a caller driving a model needs a
/// readable body it can hand back, not an exception.
/// </para>
/// </remarks>
public interface IPublicSqlQueryService
{
    /// <summary>
    /// Runs <paramref name="request"/> as <paramref name="userId"/> within <paramref name="companyId"/>.
    /// </summary>
    /// <param name="isTableInKeyScope">
    /// Whether the calling API key is scoped to read a given table. Passed in rather than resolved here so
    /// this service stays free of the HTTP-layer key entity, while still being unable to skip the check.
    /// </param>
    /// <returns>
    /// The outcome — including an in-band <see cref="PublicSqlQueryResponse.ErrorCode"/> for a SQL or
    /// permission problem inside the dataset — or <c>null</c> when the dataset does not exist, belongs to
    /// another company, or is not shared with the acting user. Those three are deliberately
    /// indistinguishable to the caller.
    /// </returns>
    Task<PublicSqlQueryResponse?> RunAsync(
        string companyId,
        string userId,
        string datasetId,
        PublicSqlQueryRequest request,
        Func<string, bool> isTableInKeyScope,
        CancellationToken ct = default);
}

public class PublicSqlQueryService : IPublicSqlQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly IDatasetService _datasets;
    private readonly IDuckdbService _duckdb;
    private readonly IDatabaseTableService _dbTables;
    private readonly IDatasetDocService _docs;
    private readonly ISqlTableResolver _resolver;
    private readonly PublicApiOptions _options;

    public PublicSqlQueryService(
        ApplicationDbContext db,
        IDatasetService datasets,
        IDuckdbService duckdb,
        IDatabaseTableService dbTables,
        IDatasetDocService docs,
        ISqlTableResolver resolver,
        PublicApiOptions options)
    {
        _db = db;
        _datasets = datasets;
        _duckdb = duckdb;
        _dbTables = dbTables;
        _docs = docs;
        _resolver = resolver;
        _options = options;
    }

    /// <summary>
    /// Functions and namespaces refused outright. This is the mitigation for the sharpest hole in
    /// model-generated SQL against DuckDB: <c>SELECT * FROM read_csv('C:/…/appsettings.json')</c> is a
    /// pure SELECT that reads the filesystem, and with <c>httpfs</c> loaded it exfiltrates.
    /// </summary>
    /// <remarks>
    /// A denylist is a mitigation, not a guarantee — the real control is opening the DuckDB handle with
    /// <c>enable_external_access=false</c>, which refuses the whole family at the engine level
    /// (verified against DuckDB 1.3: <c>read_csv</c>, <c>read_parquet</c>, <c>read_text</c>,
    /// <c>read_blob</c>, <c>glob</c>, <c>COPY TO</c>, <c>ATTACH</c>, <c>INSTALL</c>, <c>LOAD</c> and
    /// <c>https://</c> reads all fail, and the setting cannot be turned back on at runtime). This list
    /// exists so the caller gets a clear reason instead of an engine error, and so the non-DuckDB paths
    /// are covered too. <c>information_schema</c> and <c>pg_catalog</c> are here because they enumerate
    /// tables and columns outside the user's grants.
    /// </remarks>
    private static readonly string[] ForbiddenFunctions =
    {
        "read_csv", "read_csv_auto", "read_parquet", "read_json", "read_json_auto", "read_json_objects",
        "read_text", "read_blob", "read_ndjson", "sniff_csv", "glob", "parquet_scan", "csv_scan",
        "duckdb_", "sqlite_", "postgres_", "mysql_", "iceberg_", "delta_"
    };

    /// <summary>
    /// Namespaces and URL schemes refused wherever they appear. <c>information_schema</c> and
    /// <c>pg_catalog</c> enumerate tables and columns outside the user's grants.
    /// </summary>
    private static readonly string[] ForbiddenNamespaces =
    {
        "information_schema", "pg_catalog", "httpfs", "s3://", "http://", "https://"
    };

    public async Task<PublicSqlQueryResponse?> RunAsync(
        string companyId,
        string userId,
        string datasetId,
        PublicSqlQueryRequest request,
        Func<string, bool> isTableInKeyScope,
        CancellationToken ct = default)
    {
        var response = new PublicSqlQueryResponse { Sql = request?.Sql ?? string.Empty };

        if (string.IsNullOrWhiteSpace(request?.Sql))
            return Fail(response, PublicSqlErrorCodes.MissingSql, "A SQL query is required.");

        if (request.Sql.Length > _options.EffectiveMaxSqlLength)
            return Fail(response, PublicSqlErrorCodes.SqlTooLong,
                $"The query is {request.Sql.Length} characters; the limit is {_options.EffectiveMaxSqlLength}.");

        // The dataset must belong to the key's company AND be visible to the acting user.
        //
        // The company check is not redundant: DatasetService.GetDatasetAsync filters on the dataset id
        // plus (creator OR a DatasetUser row) and deliberately does NOT filter by company. That is safe
        // for the cookie-authenticated workbench, whose principal belongs to one company, but here the
        // acting user is asserted by the caller — so without this a key for company A could read
        // company B's data by naming a user who has a grant there.
        var dataset = await _datasets.GetDatasetAsync(datasetId, userId);
        if (dataset == null || !string.Equals(dataset.CompanyId, companyId, StringComparison.Ordinal))
            return null; // 404: nonexistent, another company's, or not shared with this user

        var snapshotMode = request.SnapshotMode ?? dataset.SourceType != DatasetSourceType.External;
        response.SnapshotMode = snapshotMode;
        response.RowCap = _options.ResolveRowCap(request.MaxRows);

        // Single read-only statement. Runs before the engine's own classification so the caller gets the
        // accurate reason ("multiple statements") rather than DuckDB's "requires edit permission".
        if (!SelectOnlyGuard.IsSafeSelect(request.Sql, out var guardError))
        {
            var code = guardError!.Contains("single SQL statement", StringComparison.OrdinalIgnoreCase)
                ? PublicSqlErrorCodes.MultipleStatements
                : guardError.Contains("Unterminated", StringComparison.OrdinalIgnoreCase)
                    ? PublicSqlErrorCodes.SqlError
                    : PublicSqlErrorCodes.NotASelect;
            return Fail(response, code, guardError);
        }

        var scan = SqlText.Scan(request.Sql);
        if (scan.Error is not null)
            return Fail(response, PublicSqlErrorCodes.SqlError, scan.Error);

        if (FindForbiddenToken(scan.Masked) is { } forbidden)
            return Fail(response, PublicSqlErrorCodes.ForbiddenFunction,
                $"'{forbidden}' is not permitted in a query. Only the dataset's own tables can be read.");

        var resolution = await _resolver.ResolveAsync(dataset, userId, companyId, request.Sql, snapshotMode, ct);
        if (resolution.SchemaReadFailed)
            return Fail(response, PublicSqlErrorCodes.SchemaUnavailable,
                "The dataset's tables could not be read, so access cannot be verified. Try again shortly.");

        // What the caller may query = the user's table grants ∩ the API key's dataset/table scope ∩ the
        // curated (documented) set. The key-scope intersection is not a nicety: ApiKeyScope.TableName can
        // narrow a key to specific tables, and without it a key scoped to one table would grant SQL over
        // every table its acting user can see. The documented-set intersection keeps "what the caller was
        // told exists" and "what the caller may query" identical — the data catalog only advertises
        // documented tables.
        var documented = (await _docs.GetDocumentedTablesAsync(companyId, datasetId, snapshotMode, ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var queryable = resolution.AllTables
            .Where(t => resolution.Allows(t))
            .Where(t => isTableInKeyScope(t))
            .Where(t => documented.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Live external source: the query runs against it as written, with no secured-relation rewrite.
        // Needed before the reference loop because it decides whether a qualified name is a bypass attempt
        // or simply this dataset's own naming.
        var isExternalLive = dataset.SourceType == DatasetSourceType.External && !snapshotMode;

        // Strict reference check. Unlike the workbench's permissive policy, anything unidentifiable is
        // refused here: the caller was handed the exact table list, so a name outside it is a mistake or
        // an attempt, never an alias we failed to recognise.
        var referencedTables = new List<string>();
        foreach (var reference in resolution.References)
        {
            if (resolution.CteNames.Contains(reference.Name)) continue;

            if (reference.IsFunctionCall)
                return Fail(response, PublicSqlErrorCodes.ForbiddenFunction,
                    $"'{reference.Name}(...)' is a function, not one of this dataset's tables. " +
                    "Only the tables listed for this dataset can be read.");

            // A CTE cannot shadow a schema-qualified name, so on the secured path a qualified reference
            // would read the base table and bypass masking and RLS entirely (verified against DuckDB 1.3).
            // The snapshot is single-schema, so requiring unqualified names there costs the caller nothing.
            //
            // Only on the secured path, though. An External dataset queried live is NOT rewritten into
            // secured relations, and its own catalog names are "{schema}.{name}" — so every table it has
            // is qualified, and refusing them here would make such a dataset entirely unqueryable rather
            // than merely restricted.
            if (reference.IsQualified && !isExternalLive)
                return Fail(response, PublicSqlErrorCodes.QualifiedReferenceNotAllowed,
                    $"Reference '{reference.Raw}' is schema-qualified. Use the bare table name instead.");

            if (reference.ResolvedTable is null)
                return Fail(response, PublicSqlErrorCodes.UnknownTable,
                    $"Table '{reference.Name}' is not present in the data catalog for this dataset. " +
                    "It may not exist, or it may exist but have no column documentation yet.");

            if (!queryable.Contains(reference.ResolvedTable))
                return Fail(response, PublicSqlErrorCodes.TableNotPermitted,
                    $"Access to table '{reference.ResolvedTable}' is not permitted.");

            if (!referencedTables.Contains(reference.ResolvedTable, StringComparer.OrdinalIgnoreCase))
                referencedTables.Add(reference.ResolvedTable);
        }

        response.TablesReferenced = referencedTables;

        // ---- column grants + RLS -----------------------------------------------------------------

        var columnGrants = (await _db.DatasetUserColumn
                .Where(c => c.CompanyId == companyId && c.UserId == userId && c.DatasetId == datasetId)
                .ToListAsync(ct))
            .GroupBy(c => c.TableName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => g.Select(x => x.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var rlsRows = await _db.UserRlsFilter
            .Where(r => r.CompanyId == companyId && r.UserId == userId && r.DatasetId == datasetId)
            .ToListAsync(ct);

        var hasRestrictions = columnGrants.Count > 0 || rlsRows.Count > 0;

        // External live sources cannot be secured by CTE shadowing: their table names are
        // "{schema}.{name}", a CTE cannot be named that, and a CTE named "orders" does not shadow
        // "dbo.orders". Table-level grants still work (they need no rewrite), so only the narrow case of
        // an actually-restricted user on the live path is refused.
        if (isExternalLive && hasRestrictions)
            return Fail(response, PublicSqlErrorCodes.SecurityNotEnforceable,
                "Column or row security for this dataset cannot be enforced against the live source. " +
                "Retry with \"snapshotMode\": true to query the local snapshot instead.");

        var relations = new List<SecuredSqlBuilder.SecuredRelation>();
        var maskedColumns = new List<string>();
        var appliedFilters = new List<PublicSqlRowFilterDto>();

        if (!isExternalLive)
        {
            foreach (var table in referencedTables)
            {
                List<Column> liveColumns;
                try
                {
                    liveColumns = await _duckdb.GetTableColumnsAsync(datasetId, table);
                }
                catch (Exception ex)
                {
                    return Fail(response, PublicSqlErrorCodes.SchemaUnavailable,
                        $"The columns of table '{table}' could not be read, so access cannot be verified. ({ex.Message})");
                }

                // Fail closed: an empty column list is indistinguishable from a failed read, and treating
                // it as "nothing to restrict" would turn a storage error into an access bypass.
                if (liveColumns.Count == 0)
                    return Fail(response, PublicSqlErrorCodes.SchemaUnavailable,
                        $"The columns of table '{table}' could not be read, so access cannot be verified.");

                var granted = columnGrants.TryGetValue(table, out var allowed)
                    ? liveColumns.Where(c => allowed.Contains(c.Name)).Select(c => c.Name).ToList()
                    : liveColumns.Select(c => c.Name).ToList();

                foreach (var denied in liveColumns.Select(c => c.Name)
                             .Where(name => !granted.Contains(name, StringComparer.OrdinalIgnoreCase)))
                    maskedColumns.Add($"{table}.{denied}");

                if (granted.Count == 0)
                    return Fail(response, PublicSqlErrorCodes.ColumnNotPermitted,
                        $"You have no readable columns in table '{table}'.");

                // An RLS filter names a column but not a table, so it applies to every referenced table
                // that has a column of that name. Both directions of that are wrong and neither can be
                // fixed here: a filter on a common name ("region", "year") applies to tables where it
                // means something else, and a table without the column is left unfiltered. The applied
                // set is echoed back in Security.RowFilters so a caller can see what actually happened;
                // the real fix is a table_name column on user_rls_filter.
                var predicates = new List<SecuredSqlBuilder.RlsPredicate>();
                foreach (var rls in rlsRows)
                {
                    var column = liveColumns.FirstOrDefault(c =>
                        string.Equals(c.Name, rls.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (column is null) continue;

                    if (!TryParseAllowedValues(rls.AllowedValues, out var values))
                        return Fail(response, PublicSqlErrorCodes.SecurityNotEnforceable,
                            $"A row-security filter on column '{rls.ColumnName}' is not valid configuration, " +
                            "so this query cannot be run safely. Ask an administrator to review it.");

                    if (values.Any(v => !SecuredSqlBuilder.IsSafeLiteral(v)))
                        return Fail(response, PublicSqlErrorCodes.SecurityNotEnforceable,
                            $"A row-security filter on column '{rls.ColumnName}' contains a value that cannot be " +
                            "used safely in a query. Ask an administrator to review it.");

                    predicates.Add(new SecuredSqlBuilder.RlsPredicate(column.Name, values));
                    appliedFilters.Add(new PublicSqlRowFilterDto
                    {
                        TableName = table,
                        ColumnName = column.Name,
                        AllowedValueCount = values.Count
                    });
                }

                relations.Add(new SecuredSqlBuilder.SecuredRelation(
                    TableName: table,
                    QualifiedSource: $"main.{SecuredSqlBuilder.Quote(table)}",
                    Columns: granted,
                    Predicates: predicates));
            }
        }

        response.Security = new PublicSqlSecurityDto
        {
            MaskedColumns = maskedColumns,
            RowFilters = appliedFilters,
            ColumnMaskingApplied = maskedColumns.Count > 0,
            RowSecurityApplied = appliedFilters.Count > 0
        };

        // Advisory pre-check purely for error quality: naming a masked column produces a clear message
        // instead of a binder error. Never the enforcement — an alias could false-positive here, and the
        // secured relation is what actually makes the column unreachable.
        if (FindMaskedColumnMention(scan.Masked, maskedColumns) is { } mentioned)
            return Fail(response, PublicSqlErrorCodes.ColumnNotPermitted,
                $"Column '{mentioned}' is not readable by this user.");

        string effectiveSql;
        if (isExternalLive)
        {
            effectiveSql = request.Sql;
        }
        else if (!SecuredSqlBuilder.TryBuild(request.Sql, scan, relations, resolution.CteNames,
                     out effectiveSql, out var buildError, out var buildCode))
        {
            return Fail(response, buildCode ?? PublicSqlErrorCodes.SecurityNotEnforceable, buildError!);
        }

        response.EffectiveSql = effectiveSql;

        if (!request.IncludeRows)
        {
            // Validated only: run with a one-row cap and report the column shape, discarding the row.
            // No LIMIT is appended to the SQL — that is not dialect-safe and would change the meaning of
            // a query that already has its own ORDER BY or LIMIT — so the source still evaluates the
            // query; this saves the row transfer, not the source's work.
            var probe = await ExecuteAsync(dataset, effectiveSql, 1, isExternalLive, companyId, ct);
            response.Columns = probe.Columns ?? new List<Column>();
            response.ElapsedMs = probe.ElapsedMs;
            if (!string.IsNullOrWhiteSpace(probe.Error))
                return Fail(response, PublicSqlErrorCodes.SqlError, probe.Error!);
            return response;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteAsync(dataset, effectiveSql, response.RowCap, isExternalLive, companyId, ct);
        stopwatch.Stop();

        response.ElapsedMs = result.ElapsedMs > 0 ? result.ElapsedMs : stopwatch.ElapsedMilliseconds;

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            // Both execution services capture cancellation into Error, so a timeout otherwise surfaces as
            // an opaque provider string.
            var timedOut = ct.IsCancellationRequested
                           || result.Error!.Contains("cancel", StringComparison.OrdinalIgnoreCase);
            return timedOut
                ? Fail(response, PublicSqlErrorCodes.QueryTimeout,
                    $"The query exceeded the {_options.EffectiveTimeoutSeconds}s limit. Narrow it or aggregate in SQL.")
                : Fail(response, PublicSqlErrorCodes.SqlError, result.Error!);
        }

        response.Columns = result.Columns ?? new List<Column>();
        response.Rows = result.Rows ?? new List<Dictionary<string, object?>>();
        response.RowsReturned = result.RowsReturned;
        response.Truncated = result.Truncated;

        // Post-execution assertion, not a filter. If a masked column reached the output the rewriter is
        // broken; discarding the whole result is the only safe response, and silently stripping the
        // column would hide exactly the bug that most needs to be visible.
        var leaked = response.Columns
            .Select(c => c.Name)
            .FirstOrDefault(name => maskedColumns.Any(m =>
                m.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)));
        if (leaked is not null)
        {
            response.Rows = new List<Dictionary<string, object?>>();
            response.Columns = new List<Column>();
            response.RowsReturned = 0;
            return Fail(response, PublicSqlErrorCodes.SecurityNotEnforceable,
                $"Column '{leaked}' would have been returned despite not being readable, so the result was " +
                "discarded. This is a defect — please report it.");
        }

        return response;
    }

    /// <summary>Runs the secured SQL. <c>allowWrite</c> is a literal <c>false</c>, never a variable.</summary>
    private async Task<SqlQueryResult> ExecuteAsync(Dataset dataset, string sql, int rowCap, bool useExternalSource,
        string companyId, CancellationToken ct)
    {
        if (useExternalSource)
            return await _dbTables.ExecuteQueryAsync(dataset.SourceEntityId ?? "", companyId, sql, rowCap, ct);

        return await _duckdb.ExecuteSqlAsync(dataset.Id!, sql, allowWrite: false, maxRows: rowCap, ct);
    }

    /// <summary>
    /// Parses <see cref="UserRlsFilter.AllowedValues"/> strictly.
    /// </summary>
    /// <remarks>
    /// Deliberately does <b>not</b> use <see cref="UserRlsFilter.GetAllowedValuesList"/>: that helper
    /// returns an empty list for both a legitimate <c>[]</c> and malformed JSON, and on an enforcement
    /// path those mean opposite things — "this user may see no rows" versus "this grant is corrupt and I
    /// cannot tell what it says". Conflating them is a fail-open hazard, so a parse failure returns false
    /// and the query is refused.
    /// </remarks>
    private static bool TryParseAllowedValues(string? json, out List<string> values)
    {
        values = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        values.Add(element.GetString() ?? string.Empty);
                        break;
                    case JsonValueKind.Number:
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        values.Add(element.GetRawText());
                        break;
                    default:
                        return false; // an object or nested array is not a value we can render
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The first forbidden function call or namespace in the masked SQL, or null.</summary>
    /// <remarks>
    /// Function names are only matched when they are actually <b>called</b> — the identifier must be
    /// followed by <c>(</c>. Matching them as bare words would reject any dataset that happens to have a
    /// column named <c>load</c>, <c>copy</c> or <c>glob</c>, which is entirely plausible in real data and
    /// would look like a permissions bug.
    /// <para>
    /// The statement keywords a denylist would normally carry (<c>ATTACH</c>, <c>DETACH</c>, <c>COPY</c>,
    /// <c>INSTALL</c>, <c>LOAD</c>) are deliberately absent: <see cref="SelectOnlyGuard"/> already requires
    /// a single statement beginning with SELECT or WITH, so none of them can be the statement, and
    /// including them buys nothing while causing the false positives above. Table-valued functions reached
    /// through FROM/JOIN are caught separately via <see cref="TableReferenceMatch.IsFunctionCall"/>; this
    /// check covers the other positions, such as a function in the projection.
    /// </para>
    /// </remarks>
    private static string? FindForbiddenToken(string masked)
    {
        foreach (var name in ForbiddenFunctions)
        {
            var index = masked.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var boundedLeft = index == 0 || !IsWordChar(masked[index - 1]);
                if (boundedLeft && IsCalled(masked, index + name.Length, name.EndsWith('_')))
                    return name.EndsWith('_') ? name + "…" : name;
                index = masked.IndexOf(name, index + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var ns in ForbiddenNamespaces)
            if (masked.Contains(ns, StringComparison.OrdinalIgnoreCase))
                return ns;

        return null;

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        // True when what follows is a call. For a prefix like "duckdb_", the rest of the identifier is
        // consumed first, so duckdb_settings() matches on the "duckdb_" entry.
        static bool IsCalled(string s, int i, bool isPrefix)
        {
            if (isPrefix)
                while (i < s.Length && IsWordChar(s[i])) i++;
            else if (i < s.Length && IsWordChar(s[i]))
                return false; // longer identifier, e.g. "glob_id" — not this function

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i < s.Length && s[i] == '(';
        }
    }

    /// <summary>
    /// The first masked column name mentioned as a whole word in the SQL, for a readable error. Advisory
    /// only — see the call site.
    /// </summary>
    private static string? FindMaskedColumnMention(string masked, List<string> maskedColumns)
    {
        foreach (var qualified in maskedColumns)
        {
            var column = qualified[(qualified.IndexOf('.') + 1)..];
            if (column.Length == 0) continue;

            var index = masked.IndexOf(column, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var boundedLeft = index == 0 || !IsWordChar(masked[index - 1]);
                var end = index + column.Length;
                var boundedRight = end >= masked.Length || !IsWordChar(masked[end]);
                if (boundedLeft && boundedRight) return column;
                index = masked.IndexOf(column, index + 1, StringComparison.OrdinalIgnoreCase);
            }
        }

        return null;

        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }

    private static PublicSqlQueryResponse Fail(PublicSqlQueryResponse response, string code, string message)
    {
        response.ErrorCode = code;
        response.Error = message;
        response.Rows = new List<Dictionary<string, object?>>();
        response.RowsReturned = 0;
        response.Truncated = false;
        return response;
    }
}

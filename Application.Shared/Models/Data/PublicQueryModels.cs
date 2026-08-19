using System.Collections.Generic;

namespace Application.Shared.Models.Data;

/// <summary>
/// Body of <c>POST api/dataset/{datasetId}/query/run</c> — the API-key public API's read-only SQL
/// execution endpoint, used by Relay's dataset agent to run the SQL a model wrote.
/// </summary>
public class PublicSqlQueryRequest
{
    /// <summary>The SQL to run. One statement, SELECT or WITH…SELECT.</summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// Row cap requested by the caller, clamped to the endpoint's ceiling. The applied value comes back
    /// as <see cref="PublicSqlQueryResponse.RowCap"/>.
    /// </summary>
    public int? MaxRows { get; set; }

    /// <summary>
    /// Query the dataset's local DuckDB snapshot rather than an External dataset's live source.
    /// <b>Nullable on purpose</b>, unlike <see cref="SqlQueryRequest.SnapshotMode"/>: when omitted the
    /// endpoint derives it the same way the data catalog does (<c>SourceType != External</c>), so the
    /// schema a caller was shown and the schema it queries are the same layer. Passing it explicitly
    /// when the catalog was read for the other layer is how you get "column not found" on a column you
    /// can plainly see.
    /// </summary>
    public bool? SnapshotMode { get; set; }

    /// <summary>
    /// When false, validate and report the result's columns and applied security without transferring
    /// rows — lets a caller check a candidate query cheaply.
    /// </summary>
    public bool IncludeRows { get; set; } = true;
}

/// <summary>
/// Result of a public SQL execution. Field names that overlap <see cref="SqlQueryResult"/> are identical
/// so mapping is a straight copy; <c>RowsAffected</c> and <c>IsSelect</c> are deliberately absent because
/// this endpoint only ever reads.
/// </summary>
public class PublicSqlQueryResponse
{
    /// <summary>The SQL exactly as submitted.</summary>
    public string Sql { get; set; } = string.Empty;

    /// <summary>
    /// The SQL that actually ran, after column masking and row-level security were applied by rewriting
    /// each table into a secured relation. Empty when the query was rejected before execution.
    /// </summary>
    /// <remarks>
    /// This is the most useful field for a model-driven caller: when the model writes
    /// <c>WHERE salary &gt; 100</c> and gets "column not found", the effective SQL shows it that
    /// <c>salary</c> was never in the relation it queried.
    /// </remarks>
    public string EffectiveSql { get; set; } = string.Empty;

    public List<Column> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int RowsReturned { get; set; }

    /// <summary>
    /// True when at least one more row existed beyond the <see cref="RowCap"/> rows returned. Not a total
    /// count, and it reflects this endpoint's cap rather than the engine's. Counts <b>secured</b> rows:
    /// row-level security is applied inside the secured relation, i.e. before the cap.
    /// </summary>
    public bool Truncated { get; set; }

    /// <summary>The row cap actually applied, after clamping <see cref="PublicSqlQueryRequest.MaxRows"/>.</summary>
    public int RowCap { get; set; }

    public long ElapsedMs { get; set; }

    /// <summary>The resolved snapshot mode, so a caller can tell which layer it just queried.</summary>
    public bool SnapshotMode { get; set; }

    /// <summary>Dataset tables the query referenced, in catalog casing.</summary>
    public List<string> TablesReferenced { get; set; } = new();

    /// <summary>What column masking and row-level security did to this query.</summary>
    public PublicSqlSecurityDto Security { get; set; } = new();

    /// <summary>
    /// Human-readable failure. A rejection for a SQL or permission reason inside the dataset is reported
    /// here with HTTP 200, matching how <see cref="SqlQueryResult"/> has always behaved — an agent loop
    /// needs a readable body it can feed back to the model, not an exception.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>Machine-readable failure code from <see cref="PublicSqlErrorCodes"/>.</summary>
    public string? ErrorCode { get; set; }
}

/// <summary>What the endpoint enforced for this query, so a caller never has to assume.</summary>
public class PublicSqlSecurityDto
{
    /// <summary>
    /// Columns excluded from the secured relations, as <c>table.column</c>. Discloses that a column
    /// exists without disclosing its values — consistent with
    /// <c>GET api/dataset/{id}/column-access</c>, which already names them.
    /// </summary>
    public List<string> MaskedColumns { get; set; } = new();

    /// <summary>Row filters applied, by table and column. Values are counted, never echoed.</summary>
    public List<PublicSqlRowFilterDto> RowFilters { get; set; } = new();

    public bool ColumnMaskingApplied { get; set; }
    public bool RowSecurityApplied { get; set; }
}

/// <summary>A row-level security filter that was applied to one table.</summary>
public class PublicSqlRowFilterDto
{
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>How many values were allowed. The values themselves are not returned.</summary>
    public int AllowedValueCount { get; set; }
}

/// <summary>
/// Machine-readable rejection reasons. A caller driving a language model branches on these — the model
/// needs "you named a column you may not read" to be distinguishable from "your SQL does not parse".
/// </summary>
public static class PublicSqlErrorCodes
{
    public const string MissingSql = "missing_sql";
    public const string SqlTooLong = "sql_too_long";
    public const string NotASelect = "not_a_select";
    public const string MultipleStatements = "multiple_statements";
    public const string ForbiddenFunction = "forbidden_function";
    public const string QualifiedReferenceNotAllowed = "qualified_reference_not_allowed";
    public const string UnknownTable = "unknown_table";
    public const string TableNotPermitted = "table_not_permitted";
    public const string ColumnNotPermitted = "column_not_permitted";
    public const string CteNameConflict = "cte_name_conflict";
    public const string SecurityNotEnforceable = "security_not_enforceable";
    public const string SchemaUnavailable = "schema_unavailable";
    public const string QueryTimeout = "query_timeout";
    public const string SqlError = "sql_error";
    public const string NotAMember = "not_a_member";
    public const string MissingRole = "missing_role";
}

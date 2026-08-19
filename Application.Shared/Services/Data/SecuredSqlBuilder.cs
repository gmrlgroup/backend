using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Application.Shared.Services.Data;

/// <summary>
/// Rewrites a user's read-only SQL so that every dataset table it references is replaced by a
/// <b>secured relation</b>: a CTE that projects only the columns the acting user may read and applies
/// their row-level-security predicate at the leaf.
/// </summary>
/// <remarks>
/// This is the enforcement mechanism for column masking and RLS on the public query endpoint, and it is
/// built this way because the alternatives do not work:
/// <list type="bullet">
/// <item>
/// <b>Injecting predicates into the user's SQL</b> is unsound at the text level. There may be no
/// <c>WHERE</c> to extend; <c>UNION</c> arms, subqueries and CTEs each need their own; and adding a
/// predicate to the <c>WHERE</c> of an <c>OUTER JOIN</c> silently turns it into an inner join.
/// </item>
/// <item>
/// <b>Wrapping the query</b> — <c>SELECT * FROM (user_sql) t WHERE region IN (…)</c> — fails on the most
/// ordinary analytical query there is. <c>SELECT SUM(amount) FROM orders</c> has already collapsed the
/// rows before the wrapper sees them, and <c>region</c> is not in the projection, so the wrapper either
/// errors or filters nothing. It looks like the safest option and is the least safe.
/// </item>
/// <item>
/// <b>Post-filtering the returned columns</b> is disclosure control, not read authorization: it leaks
/// through computed expressions (<c>SELECT salary*2 AS x</c>), through predicates
/// (<c>WHERE salary &gt; 200</c> filters rows by a column the user cannot read), and through aggregates
/// (<c>AVG(salary)</c> is a single number that reveals it).
/// </item>
/// </list>
/// <para>
/// Substituting the relation instead changes what the table <i>is</i> rather than what the query
/// <i>says</i>, so correctness follows from SQL semantics: <c>SELECT *</c> expands to granted columns,
/// a masked column fails to bind, and <c>COUNT(*)</c> counts filtered rows. Verified against DuckDB
/// 1.3: an <c>orders</c> CTE shadowing <c>orders</c> makes <c>COUNT(*)</c> return the filtered count,
/// aliases (<c>FROM orders o</c>) keep working, and a masked column raises a binder error.
/// </para>
/// <para>
/// <b>The one bypass, and why rule 1 below is not optional:</b> a CTE does not shadow a
/// schema-qualified name. Also verified against DuckDB 1.3 — with an <c>orders</c> CTE in scope,
/// <c>SELECT COUNT(*) FROM main.orders</c> reads straight past it and returns the unfiltered count. Any
/// qualified reference to a known dataset table must therefore be refused outright.
/// </para>
/// </remarks>
public static class SecuredSqlBuilder
{
    /// <summary>A row-level-security predicate: one column restricted to a set of values.</summary>
    /// <param name="ColumnName">Column name in catalog casing.</param>
    /// <param name="AllowedValues">
    /// The permitted values. An <b>empty</b> list means no rows are permitted and renders as
    /// <c>1 = 0</c> — never "no filter". Callers must not reach this with an empty list that actually
    /// came from malformed configuration; see the remarks on <see cref="TryBuild"/>.
    /// </param>
    public sealed record RlsPredicate(string ColumnName, IReadOnlyList<string> AllowedValues);

    /// <summary>One table, reduced to what the acting user may see of it.</summary>
    /// <param name="TableName">Table name in catalog casing.</param>
    /// <param name="QualifiedSource">
    /// How to address the real table inside the secured relation, e.g. <c>main."orders"</c>. Must be
    /// qualified, or the CTE would reference itself and recurse.
    /// </param>
    /// <param name="Columns">Granted columns in catalog casing. Empty is invalid — a table with no
    /// readable columns must be refused before it gets here.</param>
    /// <param name="Predicates">Row filters to apply at the leaf.</param>
    public sealed record SecuredRelation(
        string TableName,
        string QualifiedSource,
        IReadOnlyList<string> Columns,
        IReadOnlyList<RlsPredicate> Predicates);

    /// <summary>
    /// Builds the effective SQL. Returns false with an <paramref name="errorCode"/> from
    /// <see cref="Application.Shared.Models.Data.PublicSqlErrorCodes"/> when the query cannot be secured
    /// — in which case the caller must run nothing at all.
    /// </summary>
    /// <param name="userSql">The user's SQL, already known to be a single SELECT/WITH statement.</param>
    /// <param name="scan">The scan of <paramref name="userSql"/>, so the leading keyword and comment
    /// positions are consistent with everything else that inspected it.</param>
    /// <param name="relations">One entry per referenced dataset table.</param>
    /// <param name="userCteNames">CTE names the user's own query declares.</param>
    /// <remarks>
    /// Fails closed by design. Every branch that cannot produce a provably-secured statement returns
    /// false rather than something approximate: an operator who believes RLS is applied and is wrong is
    /// worse off than one who gets an error, because the first stops checking.
    /// </remarks>
    public static bool TryBuild(
        string userSql,
        SqlText.SqlScan scan,
        IReadOnlyList<SecuredRelation> relations,
        IReadOnlySet<string> userCteNames,
        out string effectiveSql,
        out string? error,
        out string? errorCode)
    {
        effectiveSql = string.Empty;
        error = null;
        errorCode = null;

        if (relations.Count == 0)
        {
            // Nothing to secure (for example a query over the user's own CTEs only). Run it unchanged.
            effectiveSql = userSql;
            return true;
        }

        foreach (var relation in relations)
        {
            if (relation.Columns.Count == 0)
            {
                error = $"You have no readable columns in table '{relation.TableName}'.";
                errorCode = Application.Shared.Models.Data.PublicSqlErrorCodes.ColumnNotPermitted;
                return false;
            }

            // Rule 3: a user CTE named like a secured relation would make shadowing order decide who
            // sees what. Refuse instead of reasoning about it.
            if (userCteNames.Contains(relation.TableName))
            {
                error =
                    $"Your query defines a CTE named '{relation.TableName}', which collides with the table of the " +
                    "same name. Rename the CTE.";
                errorCode = Application.Shared.Models.Data.PublicSqlErrorCodes.CteNameConflict;
                return false;
            }
        }

        var securedCtes = relations.Select(RenderRelation).ToList();

        // Rule 2: merge with the user's own WITH list rather than nesting, because a CTE can only shadow
        // a base table from the top-level WITH clause.
        var statements = SqlText.SplitStatements(scan);
        if (statements.Count != 1)
        {
            error = "Only a single SQL statement is allowed.";
            errorCode = Application.Shared.Models.Data.PublicSqlErrorCodes.MultipleStatements;
            return false;
        }

        var (offset, length) = statements[0];
        var firstKeyword = SqlText.FirstKeyword(scan.Masked, offset, length);

        if (firstKeyword == "WITH")
        {
            // Find the end of the user's WITH keyword (and RECURSIVE, if present) in the ORIGINAL text,
            // then splice: WITH [RECURSIVE] <ours>, <theirs>.
            var afterWith = SkipKeyword(scan.Masked, offset, length, "WITH");
            if (afterWith < 0)
            {
                error = "Could not interpret the WITH clause of your query.";
                errorCode = Application.Shared.Models.Data.PublicSqlErrorCodes.SqlError;
                return false;
            }

            var recursive = false;
            var afterRecursive = SkipKeyword(scan.Masked, afterWith, offset + length - afterWith, "RECURSIVE");
            if (afterRecursive >= 0)
            {
                recursive = true;
                afterWith = afterRecursive;
            }

            var prefix = recursive ? "WITH RECURSIVE " : "WITH ";
            effectiveSql = prefix + string.Join(",\n     ", securedCtes) + ",\n"
                           + userSql[afterWith..];
        }
        else
        {
            effectiveSql = "WITH " + string.Join(",\n     ", securedCtes) + "\n"
                           + userSql[offset..];
        }

        return true;
    }

    /// <summary>Renders one secured relation as a CTE.</summary>
    private static string RenderRelation(SecuredRelation relation)
    {
        var columns = string.Join(",", relation.Columns.Select(Quote));
        var sb = new StringBuilder();
        sb.Append(Quote(relation.TableName)).Append(" AS (SELECT ").Append(columns)
          .Append(" FROM ").Append(relation.QualifiedSource);

        if (relation.Predicates.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(string.Join(" AND ", relation.Predicates.Select(RenderPredicate)));
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// Renders one row filter. An empty allowed-value set becomes <c>1 = 0</c>: the grant says this user
    /// may see no values of this column, so they may see no rows.
    /// </summary>
    /// <remarks>
    /// Values are inlined rather than parameterised because neither execution service accepts
    /// parameters — both take a bare SQL string. Numeric sets are emitted bare so an index seek is still
    /// possible; anything else is emitted as quoted text with <c>'</c> doubled. Deliberately <b>not</b>
    /// wrapped in <c>CAST(col AS VARCHAR)</c>: that would make typing uniform at the cost of every index
    /// on the column, which on a large fact table is the difference between milliseconds and minutes.
    /// </remarks>
    private static string RenderPredicate(RlsPredicate predicate)
    {
        if (predicate.AllowedValues.Count == 0) return "1 = 0";

        var column = Quote(predicate.ColumnName);
        var allNumeric = predicate.AllowedValues.All(IsNumericLiteral);
        var rendered = predicate.AllowedValues.Select(v => allNumeric ? v.Trim() : QuoteLiteral(v));
        return $"{column} IN ({string.Join(",", rendered)})";
    }

    /// <summary>True when a value can be emitted as a bare numeric literal.</summary>
    public static bool IsNumericLiteral(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// Quotes an identifier for DuckDB/Postgres-style dialects, doubling any embedded quote. Callers must
    /// only pass catalog-sourced names.
    /// </summary>
    public static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    /// <summary>Quotes a string literal, doubling embedded single quotes.</summary>
    public static string QuoteLiteral(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>
    /// True when a value is safe to inline as a literal. Control characters are refused rather than
    /// escaped: they have no legitimate place in a grant value, and a value that needs escaping beyond
    /// quote-doubling is a sign the grant itself is wrong.
    /// </summary>
    public static bool IsSafeLiteral(string? value) =>
        value is not null && !value.Any(char.IsControl);

    /// <summary>
    /// If the span at <paramref name="offset"/> begins with <paramref name="keyword"/>, returns the index
    /// just past it; otherwise -1.
    /// </summary>
    private static int SkipKeyword(string masked, int offset, int length, string keyword)
    {
        var i = offset;
        var end = Math.Min(masked.Length, offset + length);
        while (i < end && char.IsWhiteSpace(masked[i])) i++;

        var start = i;
        while (i < end && (char.IsLetter(masked[i]) || masked[i] == '_')) i++;
        if (i == start) return -1;

        return string.Equals(masked[start..i], keyword, StringComparison.OrdinalIgnoreCase) ? i : -1;
    }
}

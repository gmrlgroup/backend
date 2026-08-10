using System;

namespace Application.Shared.Services.Data;

/// <summary>
/// Defense-in-depth check that a piece of SQL is a single read-only statement before it is executed
/// or persisted as a dashboard widget. This is NOT the only boundary — the DuckDB/ClickHouse execution
/// paths run read-only with timeouts and row caps regardless — but it rejects obviously-unsafe model
/// output early (DDL/DML, stacked statements, comment-hidden second statements).
/// </summary>
/// <remarks>
/// Statement splitting is delegated to <see cref="SqlText.Scan"/> so that a semicolon inside a string
/// literal or a quoted identifier is not mistaken for a statement break. The naive split this replaced
/// rejected <c>SELECT STRING_AGG(x, ';')</c> as "multiple statements" — a false positive that a language
/// model trips over constantly, and one that pushed callers toward loosening the guard rather than fixing
/// it. Nothing about what this *accepts* has been widened: a genuine second statement is still refused,
/// and an unterminated quote is now refused too rather than being scanned past.
/// </remarks>
public static class SelectOnlyGuard
{
    /// <summary>Returns true when <paramref name="sql"/> is a single SELECT/WITH statement.</summary>
    public static bool IsSafeSelect(string? sql, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(sql))
        {
            error = "Query is empty.";
            return false;
        }

        var scan = SqlText.Scan(sql);
        if (scan.Error is not null)
        {
            error = scan.Error;
            return false;
        }

        // Reject stacked statements. A single trailing semicolon yields one statement, not two.
        var statements = SqlText.SplitStatements(scan);
        if (statements.Count != 1)
        {
            error = "Only a single SQL statement is allowed.";
            return false;
        }

        var (offset, length) = statements[0];
        var firstWord = SqlText.FirstKeyword(scan.Masked, offset, length);
        if (firstWord is not ("SELECT" or "WITH"))
        {
            error = "Only read-only SELECT queries are allowed.";
            return false;
        }

        return true;
    }
}

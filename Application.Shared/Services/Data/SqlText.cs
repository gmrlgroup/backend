using System;
using System.Collections.Generic;

namespace Application.Shared.Services.Data;

/// <summary>
/// A single-pass lexical scan of a SQL string, shared by every component that has to reason about SQL
/// text without parsing it: <see cref="SelectOnlyGuard"/>, <see cref="SqlTableResolver"/> and
/// <see cref="SecuredSqlBuilder"/>.
/// </summary>
/// <remarks>
/// These components all need the same two things — "where do statements end" and "which characters are
/// real SQL rather than the inside of a comment or a string" — and getting either wrong is a security
/// bug, not a cosmetic one. Two concrete failures this exists to prevent:
/// <para>
/// Splitting on <c>;</c> naively rejects <c>SELECT STRING_AGG(x, ';')</c> as multiple statements, which
/// a language model emits constantly. Stripping <c>--</c> comments naively turns
/// <c>SELECT 'a--b' FROM secret</c> into <c>SELECT 'a</c>, hiding the <c>FROM secret</c> reference from
/// the table allow-list entirely — a query that reads a forbidden table while looking clean.
/// </para>
/// <para>
/// The mask is deliberately <b>the same length</b> as the input, so every offset taken from it still
/// points at the corresponding character of the original. That is what lets a caller find a table
/// reference in the mask and then splice or quote the real text at that position.
/// </para>
/// </remarks>
public static class SqlText
{
    /// <summary>The result of scanning a SQL string.</summary>
    /// <param name="Masked">
    /// Same-length copy of the input in which the bodies of comments and the contents of string literals
    /// are replaced by spaces. Quoted identifiers (<c>"x"</c>, <c>[x]</c>, <c>`x`</c>) keep their contents,
    /// because those are genuine table and column references that callers must still be able to match.
    /// </param>
    /// <param name="StatementSeparators">
    /// Offsets of every <c>;</c> that is real SQL punctuation — i.e. not inside a literal, comment or
    /// quoted identifier.
    /// </param>
    /// <param name="Error">
    /// Non-null when the input is lexically malformed (an unterminated literal, comment or quoted
    /// identifier). Callers must treat this as a rejection: an unterminated quote means the rest of the
    /// scan is guesswork, and guessing is how a second statement hides.
    /// </param>
    public sealed record SqlScan(string Masked, IReadOnlyList<int> StatementSeparators, string? Error);

    /// <summary>Scans <paramref name="sql"/> once, producing the mask and the statement separators.</summary>
    public static SqlScan Scan(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
            return new SqlScan(string.Empty, Array.Empty<int>(), "Query is empty.");

        var masked = sql.ToCharArray();
        var separators = new List<int>();
        var i = 0;
        var n = sql.Length;

        while (i < n)
        {
            var c = sql[i];

            // -- line comment: blank to (but not including) the newline.
            if (c == '-' && i + 1 < n && sql[i + 1] == '-')
            {
                masked[i] = ' ';
                masked[i + 1] = ' ';
                i += 2;
                while (i < n && sql[i] != '\n' && sql[i] != '\r') masked[i++] = ' ';
                continue;
            }

            // /* block comment */ — nested, because SQL Server and DuckDB both nest them, and a
            // non-nesting scan would end the comment early and treat the tail as live SQL.
            if (c == '/' && i + 1 < n && sql[i + 1] == '*')
            {
                var depth = 0;
                var start = i;
                while (i < n)
                {
                    if (i + 1 < n && sql[i] == '/' && sql[i + 1] == '*')
                    {
                        depth++;
                        masked[i] = ' ';
                        masked[i + 1] = ' ';
                        i += 2;
                        continue;
                    }
                    if (i + 1 < n && sql[i] == '*' && sql[i + 1] == '/')
                    {
                        depth--;
                        masked[i] = ' ';
                        masked[i + 1] = ' ';
                        i += 2;
                        if (depth == 0) break;
                        continue;
                    }
                    masked[i++] = ' ';
                }
                if (depth != 0)
                    return new SqlScan(new string(masked), separators,
                        $"Unterminated block comment starting at position {start}.");
                continue;
            }

            // 'string literal' — '' is an embedded quote. Contents are blanked: they are data, and a
            // caller matching table names must never see inside them.
            if (c == '\'')
            {
                var start = i;
                i++; // keep the opening delimiter
                var closed = false;
                while (i < n)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < n && sql[i + 1] == '\'')
                        {
                            masked[i] = ' ';
                            masked[i + 1] = ' ';
                            i += 2;
                            continue;
                        }
                        i++; // keep the closing delimiter
                        closed = true;
                        break;
                    }
                    masked[i++] = ' ';
                }
                if (!closed)
                    return new SqlScan(new string(masked), separators,
                        $"Unterminated string literal starting at position {start}.");
                continue;
            }

            // Quoted identifiers. Contents are PRESERVED — "orders" and [orders] are real references —
            // but they are consumed here so a ';' or a comment marker inside one is not mistaken for
            // live punctuation.
            if (c is '"' or '[' or '`')
            {
                var close = c == '[' ? ']' : c;
                var start = i;
                i++;
                var closed = false;
                while (i < n)
                {
                    if (sql[i] == close)
                    {
                        // Doubled closing delimiter is an escape ("a""b", [a]]b], `a``b`).
                        if (i + 1 < n && sql[i + 1] == close)
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        closed = true;
                        break;
                    }
                    i++;
                }
                if (!closed)
                    return new SqlScan(new string(masked), separators,
                        $"Unterminated quoted identifier starting at position {start}.");
                continue;
            }

            if (c == ';') separators.Add(i);
            i++;
        }

        return new SqlScan(new string(masked), separators, null);
    }

    /// <summary>
    /// Splits the scan into the non-empty statements it contains, as (offset, length) spans into the
    /// original SQL. A single trailing <c>;</c> yields one statement, not two.
    /// </summary>
    public static List<(int Offset, int Length)> SplitStatements(SqlScan scan)
    {
        var spans = new List<(int, int)>();
        var start = 0;
        foreach (var sep in scan.StatementSeparators)
        {
            AddIfNotBlank(start, sep - start);
            start = sep + 1;
        }
        AddIfNotBlank(start, scan.Masked.Length - start);
        return spans;

        void AddIfNotBlank(int offset, int length)
        {
            if (length <= 0) return;
            for (var k = offset; k < offset + length; k++)
                if (!char.IsWhiteSpace(scan.Masked[k]))
                {
                    spans.Add((offset, length));
                    return;
                }
        }
    }

    /// <summary>
    /// The first SQL keyword of a span, upper-cased, or an empty string when the span opens with
    /// something that is not a bare word (a <c>(</c>, a literal, punctuation).
    /// </summary>
    public static string FirstKeyword(string masked, int offset, int length)
    {
        var i = offset;
        var end = offset + length;
        while (i < end && char.IsWhiteSpace(masked[i])) i++;

        var startOfWord = i;
        while (i < end && (char.IsLetter(masked[i]) || masked[i] == '_')) i++;
        return i > startOfWord ? masked[startOfWord..i].ToUpperInvariant() : string.Empty;
    }
}

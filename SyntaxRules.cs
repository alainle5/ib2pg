namespace IB2PG;

/// <summary>
/// Centralizes identifier transformation rules, including which database column
/// names require quoting as PostgreSQL reserved words.
/// </summary>
public static class SyntaxRules
{
    // Database column names that are PostgreSQL reserved words and must be double-quoted.
    private static readonly HashSet<string> ReservedColumnNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIMIT",
    };

    public static bool RequiresQuoting(string upperName)
        => ReservedColumnNames.Contains(upperName);

    /// <summary>
    /// Returns the double-quoted, lowercase form of a reserved column name.
    /// E.g. "LIMIT" → "\"limit\"".
    /// </summary>
    public static string QuoteColumnName(string upperName)
        => $"\"{upperName.ToLowerInvariant()}\"";

    /// <summary>
    /// Formats a column reference, quoting the name if it is a PostgreSQL reserved word.
    /// qualifier may be null for unqualified columns.
    /// </summary>
    public static string FormatColumnRef(string? qualifier, string upperName)
    {
        string col = RequiresQuoting(upperName) ? QuoteColumnName(upperName) : upperName.ToLowerInvariant();
        return qualifier is null ? col : $"{qualifier.ToLowerInvariant()}.{col}";
    }

    /// <summary>
    /// Converts the content between InterBase double-quote delimiters to a PostgreSQL
    /// single-quoted string literal. InterBase Dialect 1 uses double quotes for string
    /// values; PostgreSQL uses single quotes. Embedded single quotes are escaped by doubling.
    /// </summary>
    public static string IbDoubleQuotedToPostgresString(string content)
        => "'" + content.Replace("'", "''") + "'";
}

namespace IB2PG;

public static class TypeMapper
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["INTEGER"]          = "integer",
        ["INT"]              = "integer",
        ["SMALLINT"]         = "smallint",
        ["BIGINT"]           = "bigint",
        ["FLOAT"]            = "double precision",
        ["DOUBLE PRECISION"] = "double precision",
        ["REAL"]             = "real",
        ["NUMERIC"]          = "numeric",
        ["DECIMAL"]          = "numeric",
        ["VARCHAR"]          = "text",
        ["VARYING"]          = "text",
        ["CHAR"]             = "char",
        ["CHARACTER"]        = "char",
        ["DATE"]             = "date",
        ["TIME"]             = "time",
        ["TIMESTAMP"]        = "timestamp",
        ["BLOB"]             = "text",
        ["BOOLEAN"]          = "boolean",
    };

    public static string MapType(DataType dt)
    {
        string name = dt.TypeName.ToUpperInvariant();

        // BLOB gets a warning comment
        if (name == "BLOB")
            return "text /* BLOB: verify subtype */";

        if (!Map.TryGetValue(name, out string? pgType))
            pgType = dt.TypeName.ToLowerInvariant(); // unknown type — pass through lowercased

        // Append modifiers
        if (dt.Precision.HasValue && dt.Scale.HasValue)
            return $"{pgType}({dt.Precision},{dt.Scale})";

        if (dt.Precision.HasValue)
            return $"{pgType}({dt.Precision})";

        if (dt.Length.HasValue)
            return $"{pgType}({dt.Length})";

        return pgType;
    }
}

namespace IB2PG;

public static class FileReader
{
    public static string ReadAndNormalize(string filePath)
    {
        // Try UTF-8 first, fall back to Latin-1
        string text;
        try
        {
            text = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        }
        catch
        {
            text = File.ReadAllText(filePath, System.Text.Encoding.Latin1);
        }

        // Normalize line endings
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Strip SET TERM directives — IB-specific, not valid SQL to parse
        text = StripSetTerm(text);

        return text;
    }

    private static string StripSetTerm(string sql)
    {
        // Remove lines that start with SET TERM (case-insensitive)
        var lines = sql.Split('\n');
        var sb = new System.Text.StringBuilder(sql.Length);
        foreach (var line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("SET TERM", StringComparison.OrdinalIgnoreCase))
                continue;
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }
}

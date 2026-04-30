namespace IB2PG;

public static class RawTextTransformer
{
    /// <summary>
    /// Removes leading colons from variable references (:name → name) while
    /// leaving string literal content untouched.
    /// Uses a character state machine — no regex.
    /// </summary>
    public static string StripColons(string raw)
    {
        if (!raw.Contains(':')) return raw;

        var sb = new System.Text.StringBuilder(raw.Length);
        bool inString = false;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (inString)
            {
                sb.Append(c);
                if (c == '\'')
                {
                    // doubled quote is an escape — stay in string
                    if (i + 1 < raw.Length && raw[i + 1] == '\'')
                    {
                        sb.Append(raw[++i]);
                    }
                    else
                    {
                        inString = false;
                    }
                }
            }
            else
            {
                if (c == '\'')
                {
                    inString = true;
                    sb.Append(c);
                }
                else if (c == ':' && i + 1 < raw.Length && (char.IsLetter(raw[i + 1]) || raw[i + 1] == '_'))
                {
                    // skip the colon — the identifier that follows is appended normally
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Lowercases SQL keywords and identifiers in a raw fragment for PG style.
    /// Leaves string literal content unchanged.
    /// </summary>
    public static string LowercaseIdentifiers(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        var sb = new System.Text.StringBuilder(raw.Length);
        bool inString = false;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (inString)
            {
                sb.Append(c);
                if (c == '\'')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '\'')
                        sb.Append(raw[++i]);
                    else
                        inString = false;
                }
            }
            else
            {
                if (c == '\'')
                {
                    inString = true;
                    sb.Append(c);
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies both StripColons and LowercaseIdentifiers in one pass-friendly sequence.
    /// </summary>
    public static string Normalize(string raw)
        => LowercaseIdentifiers(StripColons(raw));
}

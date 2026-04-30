namespace IB2PG;

public sealed class ParseException : Exception
{
    public int Line   { get; }
    public int Column { get; }

    public ParseException(string message, Token token)
        : base($"[{token.Line}:{token.Column}] {message} (got '{token.RawText}')")
    {
        Line   = token.Line;
        Column = token.Column;
    }

    public ParseException(string message, int line, int col)
        : base($"[{line}:{col}] {message}")
    {
        Line   = line;
        Column = col;
    }
}

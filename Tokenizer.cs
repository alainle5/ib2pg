namespace IB2PG;

public sealed class Tokenizer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["CREATE"]           = TokenType.KwCreate,
        ["PROCEDURE"]        = TokenType.KwProcedure,
        ["RETURNS"]          = TokenType.KwReturns,
        ["AS"]               = TokenType.KwAs,
        ["BEGIN"]            = TokenType.KwBegin,
        ["END"]              = TokenType.KwEnd,
        ["DECLARE"]          = TokenType.KwDeclare,
        ["VARIABLE"]         = TokenType.KwVariable,
        ["IF"]               = TokenType.KwIf,
        ["THEN"]             = TokenType.KwThen,
        ["ELSE"]             = TokenType.KwElse,
        ["FOR"]              = TokenType.KwFor,
        ["SELECT"]           = TokenType.KwSelect,
        ["FROM"]             = TokenType.KwFrom,
        ["WHERE"]            = TokenType.KwWhere,
        ["INTO"]             = TokenType.KwInto,
        ["DO"]               = TokenType.KwDo,
        ["SUSPEND"]          = TokenType.KwSuspend,
        ["EXECUTE"]          = TokenType.KwExecute,
        ["WHILE"]            = TokenType.KwWhile,
        ["EXIT"]             = TokenType.KwExit,
        ["EXCEPTION"]        = TokenType.KwException,
        ["WHEN"]             = TokenType.KwWhen,
        ["NOT"]              = TokenType.KwNot,
        ["AND"]              = TokenType.KwAnd,
        ["OR"]               = TokenType.KwOr,
        ["NULL"]             = TokenType.KwNull,
        ["IS"]               = TokenType.KwIs,
        ["IN"]               = TokenType.KwIn,
        ["LIKE"]             = TokenType.KwLike,
        ["INSERT"]           = TokenType.KwInsert,
        ["UPDATE"]           = TokenType.KwUpdate,
        ["DELETE"]           = TokenType.KwDelete,
        ["SET"]              = TokenType.KwSet,
        ["VALUES"]           = TokenType.KwValues,
        ["INTEGER"]          = TokenType.KwInteger,
        ["VARCHAR"]          = TokenType.KwVarchar,
        ["CHAR"]             = TokenType.KwChar,
        ["DATE"]             = TokenType.KwDate,
        ["TIMESTAMP"]        = TokenType.KwTimestamp,
        ["FLOAT"]            = TokenType.KwFloat,
        ["DOUBLE"]           = TokenType.KwDouble,
        ["PRECISION"]        = TokenType.KwPrecision,
        ["NUMERIC"]          = TokenType.KwNumeric,
        ["DECIMAL"]          = TokenType.KwDecimal,
        ["SMALLINT"]         = TokenType.KwSmallint,
        ["BIGINT"]           = TokenType.KwBigint,
        ["BLOB"]             = TokenType.KwBlob,
        ["BOOLEAN"]          = TokenType.KwBoolean,
        ["JOIN"]             = TokenType.KwJoin,
        ["INNER"]            = TokenType.KwInner,
        ["LEFT"]             = TokenType.KwLeft,
        ["RIGHT"]            = TokenType.KwRight,
        ["OUTER"]            = TokenType.KwOuter,
        ["ON"]               = TokenType.KwOn,
        ["ORDER"]            = TokenType.KwOrder,
        ["BY"]               = TokenType.KwBy,
        ["GROUP"]            = TokenType.KwGroup,
        ["HAVING"]           = TokenType.KwHaving,
        ["DISTINCT"]         = TokenType.KwDistinct,
        ["ALL"]              = TokenType.KwAll,
        ["RETURNING_VALUES"] = TokenType.KwReturningValues,
    };

    private readonly string _src;
    private int _pos;
    private int _line;
    private int _col;

    public Tokenizer(string source)
    {
        _src  = source;
        _pos  = 0;
        _line = 1;
        _col  = 1;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _src.Length)
            {
                tokens.Add(MakeToken(TokenType.EndOfFile, "", "", _line, _col));
                break;
            }

            // Line comment
            if (Current == '-' && Peek() == '-')
            {
                SkipLineComment();
                continue;
            }

            // Block comment
            if (Current == '/' && Peek() == '*')
            {
                SkipBlockComment();
                continue;
            }

            var tok = ReadNext();
            if (tok is not null)
                tokens.Add(tok);
        }
        return tokens;
    }

    // ── Token readers ────────────────────────────────────────────────────────

    private Token ReadNext()
    {
        char c = Current;

        if (char.IsLetter(c) || c == '_')
            return ReadIdentifierOrKeyword();

        if (char.IsDigit(c))
            return ReadNumber();

        if (c == '\'')
            return ReadStringLiteral();

        if (c == '"')
            return ReadQuotedIdentifier();

        if (c == ':')
            return ReadColonOrColonIdent();

        return ReadOperatorOrPunct();
    }

    private Token ReadIdentifierOrKeyword()
    {
        int startLine = _line, startCol = _col;
        int start = _pos;
        while (_pos < _src.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
            Advance();

        string raw   = _src[start.._pos];
        string upper = raw.ToUpperInvariant();

        if (Keywords.TryGetValue(upper, out var kwType))
            return MakeToken(kwType, raw, upper, startLine, startCol);

        return MakeToken(TokenType.Identifier, raw, upper, startLine, startCol);
    }

    private Token ReadNumber()
    {
        int startLine = _line, startCol = _col;
        int start = _pos;
        bool isFloat = false;

        while (_pos < _src.Length && char.IsDigit(Current))
            Advance();

        if (_pos < _src.Length && Current == '.' && _pos + 1 < _src.Length && char.IsDigit(Peek()))
        {
            isFloat = true;
            Advance(); // consume '.'
            while (_pos < _src.Length && char.IsDigit(Current))
                Advance();
        }

        string raw = _src[start.._pos];
        return MakeToken(isFloat ? TokenType.FloatLiteral : TokenType.IntegerLiteral, raw, raw, startLine, startCol);
    }

    private Token ReadStringLiteral()
    {
        int startLine = _line, startCol = _col;
        int start = _pos;
        Advance(); // consume opening '
        while (_pos < _src.Length)
        {
            if (Current == '\'')
            {
                Advance();
                if (_pos < _src.Length && Current == '\'')
                    Advance(); // doubled quote escape — continue
                else
                    break;     // end of literal
            }
            else
            {
                Advance();
            }
        }
        string raw = _src[start.._pos];
        return MakeToken(TokenType.StringLiteral, raw, raw, startLine, startCol);
    }

    private Token ReadQuotedIdentifier()
    {
        int startLine = _line, startCol = _col;
        Advance(); // consume opening "
        int start = _pos;
        while (_pos < _src.Length && Current != '"')
            Advance();
        string value = _src[start.._pos];
        if (_pos < _src.Length) Advance(); // consume closing "
        string raw = $"\"{value}\"";
        return MakeToken(TokenType.QuotedIdentifier, raw, value, startLine, startCol);
    }

    private Token ReadColonOrColonIdent()
    {
        int startLine = _line, startCol = _col;
        Advance(); // consume ':'

        if (_pos < _src.Length && (char.IsLetter(Current) || Current == '_'))
        {
            int start = _pos;
            while (_pos < _src.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
                Advance();
            string name = _src[start.._pos];
            string raw  = ":" + name;
            return MakeToken(TokenType.ColonIdent, raw, name.ToUpperInvariant(), startLine, startCol);
        }

        return MakeToken(TokenType.Unknown, ":", ":", startLine, startCol);
    }

    private Token ReadOperatorOrPunct()
    {
        int startLine = _line, startCol = _col;
        char c = Current;
        Advance();

        switch (c)
        {
            case '(': return MakeToken(TokenType.LeftParen,  "(", "(", startLine, startCol);
            case ')': return MakeToken(TokenType.RightParen, ")", ")", startLine, startCol);
            case ',': return MakeToken(TokenType.Comma,      ",", ",", startLine, startCol);
            case ';': return MakeToken(TokenType.Semicolon,  ";", ";", startLine, startCol);
            case '.': return MakeToken(TokenType.Dot,        ".", ".", startLine, startCol);
            case '+': return MakeToken(TokenType.Plus,       "+", "+", startLine, startCol);
            case '-': return MakeToken(TokenType.Minus,      "-", "-", startLine, startCol);
            case '*': return MakeToken(TokenType.Star,       "*", "*", startLine, startCol);
            case '/': return MakeToken(TokenType.Slash,      "/", "/", startLine, startCol);
            case '=': return MakeToken(TokenType.Equals,     "=", "=", startLine, startCol);
            case '<':
                if (_pos < _src.Length && Current == '>')
                {
                    Advance();
                    return MakeToken(TokenType.NotEquals, "<>", "<>", startLine, startCol);
                }
                if (_pos < _src.Length && Current == '=')
                {
                    Advance();
                    return MakeToken(TokenType.LessEqual, "<=", "<=", startLine, startCol);
                }
                return MakeToken(TokenType.LessThan, "<", "<", startLine, startCol);
            case '>':
                if (_pos < _src.Length && Current == '=')
                {
                    Advance();
                    return MakeToken(TokenType.GreaterEqual, ">=", ">=", startLine, startCol);
                }
                return MakeToken(TokenType.GreaterThan, ">", ">", startLine, startCol);
            case '!':
                if (_pos < _src.Length && Current == '=')
                {
                    Advance();
                    return MakeToken(TokenType.NotEquals, "!=", "!=", startLine, startCol);
                }
                return MakeToken(TokenType.Unknown, "!", "!", startLine, startCol);
            case '|':
                if (_pos < _src.Length && Current == '|')
                {
                    Advance();
                    return MakeToken(TokenType.Concatenate, "||", "||", startLine, startCol);
                }
                return MakeToken(TokenType.Unknown, "|", "|", startLine, startCol);
            default:
                return MakeToken(TokenType.Unknown, c.ToString(), c.ToString(), startLine, startCol);
        }
    }

    // ── Skip helpers ─────────────────────────────────────────────────────────

    private void SkipWhitespace()
    {
        while (_pos < _src.Length && char.IsWhiteSpace(Current))
            Advance();
    }

    private void SkipLineComment()
    {
        while (_pos < _src.Length && Current != '\n')
            Advance();
    }

    private void SkipBlockComment()
    {
        Advance(); Advance(); // consume /*
        while (_pos < _src.Length)
        {
            if (Current == '*' && Peek() == '/')
            {
                Advance(); Advance();
                return;
            }
            Advance();
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    private char Current => _pos < _src.Length ? _src[_pos] : '\0';

    private char Peek(int offset = 1)
        => (_pos + offset) < _src.Length ? _src[_pos + offset] : '\0';

    private void Advance(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (_pos >= _src.Length) break;
            if (_src[_pos] == '\n') { _line++; _col = 1; }
            else { _col++; }
            _pos++;
        }
    }

    private static Token MakeToken(TokenType type, string raw, string value, int line, int col)
        => new(type, raw, value, line, col);
}

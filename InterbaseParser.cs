namespace IB2PG;

public sealed class InterbaseParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public InterbaseParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos    = 0;
    }

    // ── Entry point ───────────────────────────────────────────────────────────

    public List<ProcedureDefinition> ParseAll()
    {
        var procs = new List<ProcedureDefinition>();
        while (Current.Type != TokenType.EndOfFile)
        {
            if (Current.Type == TokenType.KwCreate && Peek().Type == TokenType.KwProcedure)
            {
                procs.Add(ParseProcedure());
            }
            else
            {
                Consume(); // skip unknown top-level tokens
            }
        }
        return procs;
    }

    // ── Procedure ─────────────────────────────────────────────────────────────

    private ProcedureDefinition ParseProcedure()
    {
        Expect(TokenType.KwCreate);
        Expect(TokenType.KwProcedure);

        string name = ExpectIdentifier().Value;

        var inputParams  = new List<ParameterDecl>();
        var outputParams = new List<ParameterDecl>();

        if (Current.Type == TokenType.LeftParen)
        {
            Consume(); // (
            inputParams = ParseParameterList();
            Expect(TokenType.RightParen);
        }

        if (Current.Type == TokenType.KwReturns)
        {
            Consume(); // RETURNS
            Expect(TokenType.LeftParen);
            outputParams = ParseParameterList();
            Expect(TokenType.RightParen);
        }

        Expect(TokenType.KwAs);

        var localVars = new List<VariableDecl>();
        while (Current.Type == TokenType.KwDeclare)
        {
            localVars.Add(ParseDeclareVariable());
        }

        Expect(TokenType.KwBegin);
        var body = ParseStatementList();
        Expect(TokenType.KwEnd);

        // Optional trailing semicolon after END
        TryConsume(TokenType.Semicolon, out _);

        bool hasSuspend = ContainsSuspend(body);

        return new ProcedureDefinition
        {
            Name             = name,
            InputParameters  = inputParams,
            OutputParameters = outputParams,
            LocalVariables   = localVars,
            Body             = body,
            HasSuspend       = hasSuspend,
        };
    }

    private List<ParameterDecl> ParseParameterList()
    {
        var list = new List<ParameterDecl>();
        if (Current.Type == TokenType.RightParen)
            return list;

        list.Add(ParseParameterDecl());
        while (Current.Type == TokenType.Comma)
        {
            Consume();
            list.Add(ParseParameterDecl());
        }
        return list;
    }

    private ParameterDecl ParseParameterDecl()
    {
        string name = ExpectIdentifier().Value;
        var    type = ParseDataType();
        return new ParameterDecl { Name = name, Type = type };
    }

    private VariableDecl ParseDeclareVariable()
    {
        Expect(TokenType.KwDeclare);
        Expect(TokenType.KwVariable);
        string name = ExpectIdentifier().Value;
        var    type = ParseDataType();
        TryConsume(TokenType.Semicolon, out _);
        return new VariableDecl { Name = name, Type = type };
    }

    private DataType ParseDataType()
    {
        string typeName;
        int? length = null, precision = null, scale = null;

        // DOUBLE PRECISION — two-word type
        if (Current.Type == TokenType.KwDouble)
        {
            Consume();
            if (Current.Type == TokenType.KwPrecision)
                Consume();
            typeName = "DOUBLE PRECISION";
        }
        else
        {
            typeName = ExpectIdentifier().Value;
        }

        // Optional (length) or (precision, scale)
        if (Current.Type == TokenType.LeftParen)
        {
            Consume();
            precision = int.Parse(ExpectLiteral().Value);
            if (Current.Type == TokenType.Comma)
            {
                Consume();
                scale = int.Parse(ExpectLiteral().Value);
            }
            else
            {
                length    = precision;
                precision = null;
            }
            Expect(TokenType.RightParen);
        }

        return new DataType
        {
            TypeName  = typeName,
            Length    = length,
            Precision = precision,
            Scale     = scale,
        };
    }

    // ── Statement list ────────────────────────────────────────────────────────

    private List<StatementNode> ParseStatementList()
    {
        var stmts = new List<StatementNode>();
        while (Current.Type != TokenType.KwEnd && Current.Type != TokenType.EndOfFile)
        {
            stmts.Add(ParseStatement());
        }
        return stmts;
    }

    private StatementNode ParseStatement()
    {
        switch (Current.Type)
        {
            case TokenType.KwIf:
                return ParseIf();

            case TokenType.KwFor when Peek().Type == TokenType.KwSelect:
                return ParseForSelect();

            case TokenType.KwWhile:
                return ParseWhile();

            case TokenType.KwSelect:
                return ParseSelectInto();

            case TokenType.KwExecute when Peek().Type == TokenType.KwProcedure:
                return ParseExecuteProcedure();

            case TokenType.KwSuspend:
                Consume();
                TryConsume(TokenType.Semicolon, out _);
                return new SuspendStatement();

            case TokenType.KwExit:
                Consume();
                TryConsume(TokenType.Semicolon, out _);
                return new ExitStatement();

            case TokenType.KwException:
                return ParseExceptionStatement();

            case TokenType.ColonIdent when Peek().Type == TokenType.Equals:
            case TokenType.Identifier when Peek().Type == TokenType.Equals:
                return ParseAssignment();

            default:
                return ParseRawStatement();
        }
    }

    // ── IF ────────────────────────────────────────────────────────────────────

    private IfStatement ParseIf()
    {
        Expect(TokenType.KwIf);

        // Condition — consume the parenthesised expression
        // IB requires parens; we handle them gracefully
        ExpressionNode condition;
        if (Current.Type == TokenType.LeftParen)
        {
            Consume(); // (
            condition = ParseExpression();
            Expect(TokenType.RightParen);
        }
        else
        {
            condition = ParseExpression();
        }

        Expect(TokenType.KwThen);

        var thenBody = ParseBlock();

        List<StatementNode>? elseBody = null;
        if (Current.Type == TokenType.KwElse)
        {
            Consume();
            elseBody = ParseBlock();
        }

        return new IfStatement
        {
            Condition = condition,
            ThenBody  = thenBody,
            ElseBody  = elseBody,
        };
    }

    // Parses either a BEGIN...END block or a single statement
    private List<StatementNode> ParseBlock()
    {
        if (Current.Type == TokenType.KwBegin)
        {
            Consume(); // BEGIN
            var stmts = ParseStatementList();
            Expect(TokenType.KwEnd);
            TryConsume(TokenType.Semicolon, out _);
            return stmts;
        }
        else
        {
            return [ParseStatement()];
        }
    }

    // ── FOR SELECT ────────────────────────────────────────────────────────────

    private ForSelectLoop ParseForSelect()
    {
        Expect(TokenType.KwFor);
        Expect(TokenType.KwSelect);

        // IB FOR SELECT order: SELECT cols FROM table [WHERE cond] [ORDER/GROUP...] INTO :vars DO
        var forSelectStops = new HashSet<TokenType> { TokenType.KwFrom };
        var cols = CollectColumnList(forSelectStops);

        Expect(TokenType.KwFrom);
        string fromClause = CollectFragmentUntil(new HashSet<TokenType>
            { TokenType.KwWhere, TokenType.KwInto, TokenType.KwDo,
              TokenType.KwOrder, TokenType.KwGroup });

        string? whereClause = null;
        if (Current.Type == TokenType.KwWhere)
        {
            Consume();
            whereClause = CollectFragmentUntil(new HashSet<TokenType>
                { TokenType.KwInto, TokenType.KwDo, TokenType.KwOrder, TokenType.KwGroup });
        }

        string? otherClauses = null;
        if (Current.Type == TokenType.KwOrder || Current.Type == TokenType.KwGroup)
        {
            otherClauses = CollectFragmentUntil(new HashSet<TokenType>
                { TokenType.KwInto, TokenType.KwDo });
        }

        Expect(TokenType.KwInto);
        var intoVars = ParseColonIdentList();

        Expect(TokenType.KwDo);
        var body = ParseBlock();

        return new ForSelectLoop
        {
            SelectColumns = cols,
            FromClause    = fromClause,
            WhereClause   = whereClause,
            OtherClauses  = otherClauses,
            IntoVariables = intoVars,
            Body          = body,
        };
    }

    // ── WHILE ─────────────────────────────────────────────────────────────────

    private WhileStatement ParseWhile()
    {
        Expect(TokenType.KwWhile);

        ExpressionNode condition;
        if (Current.Type == TokenType.LeftParen)
        {
            Consume();
            condition = ParseExpression();
            Expect(TokenType.RightParen);
        }
        else
        {
            condition = ParseExpression();
        }

        Expect(TokenType.KwDo);
        var body = ParseBlock();

        return new WhileStatement { Condition = condition, Body = body };
    }

    // ── SELECT INTO ───────────────────────────────────────────────────────────

    private StatementNode ParseSelectInto()
    {
        Expect(TokenType.KwSelect);

        // IB SELECT INTO order: SELECT cols INTO :vars FROM table [WHERE...] [ORDER...]
        var selectStops = new HashSet<TokenType>
            { TokenType.KwInto, TokenType.KwFrom, TokenType.Semicolon };
        var cols = CollectColumnList(selectStops);

        // If no INTO, degrade to raw
        if (Current.Type != TokenType.KwInto)
        {
            string partial = "SELECT " + string.Join(", ", cols);
            if (Current.Type == TokenType.KwFrom)
            {
                Consume();
                string rest = CollectFragmentUntil(new HashSet<TokenType> { TokenType.Semicolon });
                partial += " FROM " + rest;
            }
            TryConsume(TokenType.Semicolon, out _);
            return new RawStatement { RawSql = partial };
        }

        Expect(TokenType.KwInto);
        var intoVars = ParseColonIdentList();

        Expect(TokenType.KwFrom);
        string fromClause = CollectFragmentUntil(new HashSet<TokenType>
            { TokenType.KwWhere, TokenType.Semicolon, TokenType.KwOrder, TokenType.KwGroup, TokenType.KwHaving });

        string? whereClause = null;
        if (Current.Type == TokenType.KwWhere)
        {
            Consume();
            whereClause = CollectFragmentUntil(new HashSet<TokenType>
                { TokenType.Semicolon, TokenType.KwOrder, TokenType.KwGroup, TokenType.KwHaving });
        }

        string? otherClauses = null;
        if (Current.Type is TokenType.KwOrder or TokenType.KwGroup or TokenType.KwHaving)
        {
            otherClauses = CollectFragmentUntil(new HashSet<TokenType> { TokenType.Semicolon });
        }

        TryConsume(TokenType.Semicolon, out _);

        return new SelectIntoStatement
        {
            SelectColumns = cols,
            IntoVariables = intoVars,
            FromClause    = fromClause,
            WhereClause   = whereClause,
            OtherClauses  = otherClauses,
        };
    }

    // ── ASSIGNMENT ────────────────────────────────────────────────────────────

    private AssignmentStatement ParseAssignment()
    {
        string varName = Current.Value; // already uppercased, colon stripped
        Consume();                      // consume variable name
        Expect(TokenType.Equals);
        var value = ParseExpression();
        TryConsume(TokenType.Semicolon, out _);
        return new AssignmentStatement { VariableName = varName, Value = value };
    }

    // ── EXECUTE PROCEDURE ─────────────────────────────────────────────────────

    private ExecuteProcedureStatement ParseExecuteProcedure()
    {
        Expect(TokenType.KwExecute);
        Expect(TokenType.KwProcedure);

        string name = ExpectIdentifier().Value;

        var args = new List<ExpressionNode>();
        if (Current.Type == TokenType.LeftParen)
        {
            Consume();
            if (Current.Type != TokenType.RightParen)
            {
                args.Add(ParseExpression());
                while (Current.Type == TokenType.Comma)
                {
                    Consume();
                    args.Add(ParseExpression());
                }
            }
            Expect(TokenType.RightParen);
        }

        List<string>? intoVars = null;
        if (Current.Type == TokenType.KwReturningValues)
        {
            Consume();
            intoVars = ParseColonIdentList();
        }

        TryConsume(TokenType.Semicolon, out _);

        return new ExecuteProcedureStatement
        {
            ProcedureName = name,
            Arguments     = args,
            IntoVariables = intoVars,
        };
    }

    // ── EXCEPTION ─────────────────────────────────────────────────────────────

    private ExceptionStatement ParseExceptionStatement()
    {
        Expect(TokenType.KwException);
        string name = ExpectIdentifier().Value;
        TryConsume(TokenType.Semicolon, out _);
        return new ExceptionStatement { ExceptionName = name };
    }

    // ── RAW (fallback) ────────────────────────────────────────────────────────

    private RawStatement ParseRawStatement()
    {
        var toks = new List<string>();
        while (Current.Type != TokenType.Semicolon
            && Current.Type != TokenType.KwEnd
            && Current.Type != TokenType.EndOfFile)
        {
            toks.Add(CurrentTokenAsRaw());
            Consume();
        }
        TryConsume(TokenType.Semicolon, out _);
        return new RawStatement { RawSql = string.Join(" ", toks) };
    }

    // ── Expression parser ─────────────────────────────────────────────────────

    private ExpressionNode ParseExpression() => ParseOrExpression();

    private ExpressionNode ParseOrExpression()
    {
        var left = ParseAndExpression();
        while (Current.Type == TokenType.KwOr)
        {
            string op = "OR";
            Consume();
            var right = ParseAndExpression();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private ExpressionNode ParseAndExpression()
    {
        var left = ParseNotExpression();
        while (Current.Type == TokenType.KwAnd)
        {
            Consume();
            var right = ParseNotExpression();
            left = new BinaryExpression { Left = left, Operator = "AND", Right = right };
        }
        return left;
    }

    private ExpressionNode ParseNotExpression()
    {
        if (Current.Type == TokenType.KwNot)
        {
            Consume();
            return new UnaryExpression { Operator = "NOT", Operand = ParseNotExpression() };
        }
        return ParseComparison();
    }

    private ExpressionNode ParseComparison()
    {
        var left = ParseAddSub();

        string? op = Current.Type switch
        {
            TokenType.Equals       => "=",
            TokenType.NotEquals    => "<>",
            TokenType.LessThan     => "<",
            TokenType.GreaterThan  => ">",
            TokenType.LessEqual    => "<=",
            TokenType.GreaterEqual => ">=",
            _                      => null,
        };

        if (op != null)
        {
            Consume();
            var right = ParseAddSub();
            return new BinaryExpression { Left = left, Operator = op, Right = right };
        }

        // IS [NOT] NULL
        if (Current.Type == TokenType.KwIs)
        {
            Consume();
            bool negated = false;
            if (Current.Type == TokenType.KwNot) { Consume(); negated = true; }
            Expect(TokenType.KwNull);
            string isOp = negated ? "IS NOT NULL" : "IS NULL";
            return new BinaryExpression { Left = left, Operator = isOp, Right = new LiteralExpression { RawValue = "" } };
        }

        // [NOT] IN (...)
        if (Current.Type == TokenType.KwIn || (Current.Type == TokenType.KwNot && Peek().Type == TokenType.KwIn))
        {
            bool negated = Current.Type == TokenType.KwNot;
            if (negated) Consume();
            Consume(); // IN
            // Collect raw IN list as RawExpression
            var raw = new System.Text.StringBuilder("IN ");
            raw.Append(CurrentTokenAsRaw()); Consume(); // (
            int depth = 1;
            while (depth > 0 && Current.Type != TokenType.EndOfFile)
            {
                if (Current.Type == TokenType.LeftParen)  depth++;
                if (Current.Type == TokenType.RightParen) depth--;
                raw.Append(' ').Append(CurrentTokenAsRaw());
                Consume();
            }
            string inOp = negated ? "NOT IN" : "IN";
            return new BinaryExpression { Left = left, Operator = inOp, Right = new RawExpression { RawText = raw.ToString() } };
        }

        // [NOT] LIKE
        if (Current.Type == TokenType.KwLike || (Current.Type == TokenType.KwNot && Peek().Type == TokenType.KwLike))
        {
            bool negated = Current.Type == TokenType.KwNot;
            if (negated) Consume();
            Consume(); // LIKE
            var right = ParseAddSub();
            string likeOp = negated ? "NOT LIKE" : "LIKE";
            return new BinaryExpression { Left = left, Operator = likeOp, Right = right };
        }

        return left;
    }

    private ExpressionNode ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Current.Type is TokenType.Plus or TokenType.Minus or TokenType.Concatenate)
        {
            string op = Current.RawText;
            Consume();
            var right = ParseMulDiv();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private ExpressionNode ParseMulDiv()
    {
        var left = ParseUnary();
        while (Current.Type is TokenType.Star or TokenType.Slash)
        {
            string op = Current.RawText;
            Consume();
            var right = ParseUnary();
            left = new BinaryExpression { Left = left, Operator = op, Right = right };
        }
        return left;
    }

    private ExpressionNode ParseUnary()
    {
        if (Current.Type == TokenType.Minus)
        {
            Consume();
            return new UnaryExpression { Operator = "-", Operand = ParsePrimary() };
        }
        return ParsePrimary();
    }

    private ExpressionNode ParsePrimary()
    {
        switch (Current.Type)
        {
            case TokenType.ColonIdent:
            {
                string name = Current.Value;
                Consume();
                return new VariableExpression { Name = name };
            }

            case TokenType.Identifier:
            case TokenType.QuotedIdentifier:
            {
                string name = Current.Value;
                Consume();
                if (Current.Type == TokenType.LeftParen)
                {
                    // Function call
                    Consume();
                    var args = new List<ExpressionNode>();
                    if (Current.Type != TokenType.RightParen)
                    {
                        args.Add(ParseExpression());
                        while (Current.Type == TokenType.Comma) { Consume(); args.Add(ParseExpression()); }
                    }
                    Expect(TokenType.RightParen);
                    return new FunctionCallExpression { FunctionName = name, Arguments = args };
                }
                if (Current.Type == TokenType.Dot)
                {
                    // qualified name e.g. schema.table — collect as raw
                    Consume();
                    string field = Current.Value;
                    Consume();
                    return new VariableExpression { Name = name + "." + field };
                }
                return new VariableExpression { Name = name };
            }

            case TokenType.IntegerLiteral:
            case TokenType.FloatLiteral:
            case TokenType.StringLiteral:
            {
                string val = Current.RawText;
                Consume();
                return new LiteralExpression { RawValue = val };
            }

            case TokenType.KwNull:
                Consume();
                return new LiteralExpression { RawValue = "NULL" };

            case TokenType.LeftParen:
            {
                Consume(); // (
                var inner = ParseExpression();
                Expect(TokenType.RightParen);
                return inner;
            }

            default:
            {
                // Fallback — collect tokens that might be part of an expression
                // until we hit a sentinel
                var raw = new System.Text.StringBuilder();
                while (!IsExpressionTerminator())
                {
                    raw.Append(CurrentTokenAsRaw()).Append(' ');
                    Consume();
                }
                return new RawExpression { RawText = raw.ToString().TrimEnd() };
            }
        }
    }

    // ── Raw fragment collection ───────────────────────────────────────────────

    // Collect tokens as a single SQL fragment with proper spacing.
    // Stops when a stop token (or EOF) is reached; does not consume it.
    private string CollectFragmentUntil(HashSet<TokenType> stops)
    {
        var sb = new System.Text.StringBuilder();
        bool needSpace = false;

        while (!stops.Contains(Current.Type) && Current.Type != TokenType.EndOfFile)
        {
            TokenType t = Current.Type;

            if (needSpace
                && t != TokenType.Dot
                && t != TokenType.Comma
                && t != TokenType.RightParen
                && t != TokenType.Semicolon)
                sb.Append(' ');

            sb.Append(CurrentTokenAsRaw());
            needSpace = t != TokenType.Dot && t != TokenType.LeftParen;
            Consume();
        }

        return sb.ToString();
    }

    // Collect a comma-separated list of expressions, splitting on top-level commas.
    private List<string> CollectColumnList(HashSet<TokenType> stops)
    {
        var items = new List<string>();
        var item  = new System.Text.StringBuilder();
        bool needSpace = false;
        int depth = 0;

        while (!stops.Contains(Current.Type) && Current.Type != TokenType.EndOfFile)
        {
            TokenType t = Current.Type;

            if (t == TokenType.LeftParen)  depth++;
            if (t == TokenType.RightParen) depth--;

            if (t == TokenType.Comma && depth == 0)
            {
                items.Add(item.ToString().Trim());
                item.Clear();
                needSpace = false;
                Consume();
                continue;
            }

            if (needSpace && t != TokenType.Dot && t != TokenType.RightParen)
                item.Append(' ');

            item.Append(CurrentTokenAsRaw());
            needSpace = t != TokenType.Dot && t != TokenType.LeftParen;
            Consume();
        }

        string last = item.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(last))
            items.Add(last);

        return items;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<string> ParseColonIdentList()
    {
        var list = new List<string>();
        if (Current.Type == TokenType.ColonIdent || Current.Type == TokenType.Identifier)
        {
            list.Add(Current.Value);
            Consume();
            while (Current.Type == TokenType.Comma)
            {
                Consume();
                if (Current.Type == TokenType.ColonIdent || Current.Type == TokenType.Identifier)
                {
                    list.Add(Current.Value);
                    Consume();
                }
            }
        }
        return list;
    }

    private bool IsExpressionTerminator() => Current.Type switch
    {
        TokenType.Semicolon    => true,
        TokenType.KwThen       => true,
        TokenType.KwDo         => true,
        TokenType.KwEnd        => true,
        TokenType.KwElse       => true,
        TokenType.KwFrom       => true,
        TokenType.KwInto       => true,
        TokenType.RightParen   => true,
        TokenType.Comma        => true,
        TokenType.EndOfFile    => true,
        _                      => false,
    };

    private string CurrentTokenAsRaw()
    {
        // Use RawText to preserve original casing for SQL fragments
        // but strip colons from variable references
        return Current.Type == TokenType.ColonIdent
            ? Current.Value.ToLowerInvariant()  // colon already stripped; lowercase for PG
            : Current.RawText;
    }

    private static bool ContainsSuspend(List<StatementNode> stmts)
    {
        foreach (var s in stmts)
        {
            if (s is SuspendStatement) return true;
            if (s is IfStatement ifs)
            {
                if (ContainsSuspend(ifs.ThenBody)) return true;
                if (ifs.ElseBody != null && ContainsSuspend(ifs.ElseBody)) return true;
            }
            if (s is ForSelectLoop fsl && ContainsSuspend(fsl.Body)) return true;
            if (s is WhileStatement ws && ContainsSuspend(ws.Body)) return true;
        }
        return false;
    }

    // ── Token navigation ──────────────────────────────────────────────────────

    private Token Current => _tokens[_pos];

    private Token Peek(int offset = 1)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
    }

    private Token Consume()
    {
        var t = _tokens[_pos];
        if (_pos < _tokens.Count - 1) _pos++;
        return t;
    }

    private Token Expect(TokenType type)
    {
        if (Current.Type != type)
            throw new ParseException($"Expected {type}", Current);
        return Consume();
    }

    private Token ExpectIdentifier()
    {
        if (Current.Type is TokenType.Identifier
                         or TokenType.QuotedIdentifier
                         or TokenType.KwDate       // allow keywords as identifiers in some positions
                         or TokenType.KwTimestamp
                         or TokenType.KwInteger
                         or TokenType.KwVarchar
                         or TokenType.KwChar
                         or TokenType.KwFloat
                         or TokenType.KwDouble
                         or TokenType.KwNumeric
                         or TokenType.KwDecimal
                         or TokenType.KwSmallint
                         or TokenType.KwBigint
                         or TokenType.KwBlob
                         or TokenType.KwBoolean
                         or TokenType.KwPrecision)
            return Consume();

        throw new ParseException("Expected identifier", Current);
    }

    private Token ExpectLiteral()
    {
        if (Current.Type is TokenType.IntegerLiteral or TokenType.FloatLiteral)
            return Consume();
        throw new ParseException("Expected numeric literal", Current);
    }

    private bool TryConsume(TokenType type, out Token token)
    {
        if (Current.Type == type)
        {
            token = Consume();
            return true;
        }
        token = Current;
        return false;
    }
}

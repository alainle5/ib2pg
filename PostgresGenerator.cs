namespace IB2PG;

public sealed class PostgresGenerator
{
    private readonly System.Text.StringBuilder _sb = new();
    private int _indent;
    private const int IndentSize = 4;

    public string Generate(List<ProcedureDefinition> procedures)
    {
        _sb.Clear();
        _indent = 0;

        for (int i = 0; i < procedures.Count; i++)
        {
            EmitProcedure(procedures[i]);
            if (i < procedures.Count - 1)
                _sb.AppendLine();
        }

        return _sb.ToString();
    }

    // ── Procedure ─────────────────────────────────────────────────────────────

    private void EmitProcedure(ProcedureDefinition proc)
    {
        EmitSignature(proc);
        EmitReturnType(proc);
        AppendLine("LANGUAGE plpgsql");
        AppendLine("AS $$");

        // DECLARE block — always emit even if empty, for cleanliness
        AppendLine("DECLARE");
        PushIndent();
        foreach (var v in proc.LocalVariables)
        {
            AppendLine($"{v.Name.ToLowerInvariant()} {TypeMapper.MapType(v.Type)};");
        }
        // Output params live as DECLARE vars when not using RETURNS TABLE
        if (!proc.HasSuspend)
        {
            foreach (var p in proc.OutputParameters)
            {
                AppendLine($"{p.Name.ToLowerInvariant()} {TypeMapper.MapType(p.Type)};");
            }
        }
        PopIndent();

        AppendLine("BEGIN");
        PushIndent();

        if (proc.Body.Count == 0)
            AppendLine("NULL; -- empty procedure body");
        else
            foreach (var stmt in proc.Body)
                EmitStatement(stmt);

        PopIndent();
        AppendLine("END;");
        AppendLine("$$;");
    }

    private void EmitSignature(ProcedureDefinition proc)
    {
        string name   = proc.Name.ToLowerInvariant();
        string params_ = string.Join(", ",
            proc.InputParameters.Select(p => $"{p.Name.ToLowerInvariant()} {TypeMapper.MapType(p.Type)}"));
        AppendLine($"CREATE OR REPLACE FUNCTION {name}({params_})");
    }

    private void EmitReturnType(ProcedureDefinition proc)
    {
        if (proc.HasSuspend && proc.OutputParameters.Count > 0)
        {
            string cols = string.Join(", ",
                proc.OutputParameters.Select(p => $"{p.Name.ToLowerInvariant()} {TypeMapper.MapType(p.Type)}"));
            AppendLine($"RETURNS TABLE({cols})");
        }
        else if (!proc.HasSuspend && proc.OutputParameters.Count == 1)
        {
            AppendLine($"RETURNS {TypeMapper.MapType(proc.OutputParameters[0].Type)}");
        }
        else if (!proc.HasSuspend && proc.OutputParameters.Count > 1)
        {
            string cols = string.Join(", ",
                proc.OutputParameters.Select(p => $"{p.Name.ToLowerInvariant()} {TypeMapper.MapType(p.Type)}"));
            AppendLine($"RETURNS TABLE({cols})");
        }
        else
        {
            AppendLine("RETURNS void");
        }
    }

    // ── Statement dispatch ────────────────────────────────────────────────────

    private void EmitStatement(StatementNode stmt)
    {
        switch (stmt)
        {
            case IfStatement s:               EmitIf(s);               break;
            case ForSelectLoop s:             EmitForSelect(s);        break;
            case WhileStatement s:            EmitWhile(s);            break;
            case SelectIntoStatement s:       EmitSelectInto(s);       break;
            case AssignmentStatement s:       EmitAssignment(s);       break;
            case ExecuteProcedureStatement s: EmitExecuteProcedure(s); break;
            case SuspendStatement:            AppendLine("RETURN NEXT;");  break;
            case ExitStatement:               AppendLine("RETURN;");       break;
            case ExceptionStatement s:        EmitException(s);        break;
            case RawStatement s:              EmitRaw(s);              break;
            default:
                throw new GeneratorException($"Unknown statement node: {stmt.GetType().Name}", stmt);
        }
    }

    // ── IF ────────────────────────────────────────────────────────────────────

    private void EmitIf(IfStatement stmt)
    {
        string cond = EmitExpression(stmt.Condition);
        AppendLine($"IF {cond} THEN");
        PushIndent();
        EmitStatements(stmt.ThenBody);
        PopIndent();

        if (stmt.ElseBody is { Count: > 0 })
        {
            AppendLine("ELSE");
            PushIndent();
            EmitStatements(stmt.ElseBody);
            PopIndent();
        }

        AppendLine("END IF;");
    }

    // ── FOR SELECT ────────────────────────────────────────────────────────────

    private void EmitForSelect(ForSelectLoop stmt)
    {
        // PostgreSQL FOR loop target: plain comma-separated scalar variables (no parens)
        string vars = string.Join(", ", stmt.IntoVariables.Select(v => v.ToLowerInvariant()));

        string cols = RawTextTransformer.Normalize(string.Join(", ", stmt.SelectColumns));
        string from = RawTextTransformer.Normalize(stmt.FromClause);

        string query = $"SELECT {cols} FROM {from}";
        if (stmt.WhereClause is not null)
            query += $" WHERE {RawTextTransformer.Normalize(stmt.WhereClause)}";
        if (stmt.OtherClauses is not null)
            query += $" {RawTextTransformer.Normalize(stmt.OtherClauses)}";

        AppendLine($"FOR {vars} IN {query} LOOP");
        PushIndent();
        EmitStatements(stmt.Body);
        PopIndent();
        AppendLine("END LOOP;");
    }

    // ── WHILE ─────────────────────────────────────────────────────────────────

    private void EmitWhile(WhileStatement stmt)
    {
        AppendLine($"WHILE {EmitExpression(stmt.Condition)} LOOP");
        PushIndent();
        EmitStatements(stmt.Body);
        PopIndent();
        AppendLine("END LOOP;");
    }

    // ── SELECT INTO ───────────────────────────────────────────────────────────

    private void EmitSelectInto(SelectIntoStatement stmt)
    {
        string cols = RawTextTransformer.Normalize(string.Join(", ", stmt.SelectColumns));
        string vars = string.Join(", ", stmt.IntoVariables.Select(v => v.ToLowerInvariant()));
        string from = RawTextTransformer.Normalize(stmt.FromClause);

        string sql = $"SELECT {cols} INTO {vars} FROM {from}";
        if (stmt.WhereClause is not null)
            sql += $" WHERE {RawTextTransformer.Normalize(stmt.WhereClause)}";
        if (stmt.OtherClauses is not null)
            sql += $" {RawTextTransformer.Normalize(stmt.OtherClauses)}";

        AppendLine(sql + ";");
    }

    // ── ASSIGNMENT ────────────────────────────────────────────────────────────

    private void EmitAssignment(AssignmentStatement stmt)
    {
        string var_  = stmt.VariableName.ToLowerInvariant();
        string value = EmitExpression(stmt.Value);
        AppendLine($"{var_} := {value};");
    }

    // ── EXECUTE PROCEDURE ─────────────────────────────────────────────────────

    private void EmitExecuteProcedure(ExecuteProcedureStatement stmt)
    {
        string name = stmt.ProcedureName.ToLowerInvariant();
        string args = string.Join(", ", stmt.Arguments.Select(EmitExpression));

        if (stmt.IntoVariables is { Count: > 0 })
        {
            // Select named output columns by name and capture into local variables.
            // Assumes output param names match the RETURNING_VALUES variable names.
            string vars = string.Join(", ", stmt.IntoVariables.Select(v => v.ToLowerInvariant()));
            AppendLine($"SELECT {vars} INTO {vars} FROM {name}({args});");
        }
        else
        {
            AppendLine($"PERFORM {name}({args});");
        }
    }

    // ── EXCEPTION ─────────────────────────────────────────────────────────────

    private void EmitException(ExceptionStatement stmt)
    {
        AppendLine($"RAISE EXCEPTION '{stmt.ExceptionName.ToLowerInvariant()}';");
    }

    // ── RAW (pass-through) ────────────────────────────────────────────────────

    private void EmitRaw(RawStatement stmt)
    {
        string sql = RawTextTransformer.Normalize(stmt.RawSql);
        if (string.IsNullOrWhiteSpace(sql))
            return;
        AppendLine(sql + ";");
    }

    // ── Expression emission ───────────────────────────────────────────────────

    private string EmitExpression(ExpressionNode expr) => expr switch
    {
        LiteralExpression e       => e.RawValue,
        VariableExpression e      => e.Name.ToLowerInvariant(),
        RawExpression e           => RawTextTransformer.Normalize(e.RawText),
        UnaryExpression e         => e.Operator == "-"
                                       ? $"-{EmitExpression(e.Operand)}"
                                       : $"{e.Operator} {EmitExpression(e.Operand)}",
        FunctionCallExpression e  =>
            $"{e.FunctionName.ToLowerInvariant()}({string.Join(", ", e.Arguments.Select(EmitExpression))})",
        BinaryExpression e        => EmitBinary(e),
        _ => throw new GeneratorException($"Unknown expression node: {expr.GetType().Name}", expr),
    };

    private string EmitBinary(BinaryExpression e)
    {
        string op = e.Operator;

        // IS NULL / IS NOT NULL — right side is empty sentinel
        if (op is "IS NULL" or "IS NOT NULL")
            return $"{EmitExpression(e.Left)} {op}";

        // IN / NOT IN — right side is already a raw fragment including parens
        if (op is "IN" or "NOT IN")
            return $"{EmitExpression(e.Left)} {op} {EmitExpression(e.Right)}";

        return $"{EmitExpression(e.Left)} {op} {EmitExpression(e.Right)}";
    }

    // ── Indent helpers ────────────────────────────────────────────────────────

    private void EmitStatements(List<StatementNode> stmts)
    {
        if (stmts.Count == 0)
            AppendLine("NULL; -- empty block");
        else
            foreach (var s in stmts)
                EmitStatement(s);
    }

    private void AppendLine(string text)
        => _sb.Append(' ', _indent * IndentSize).AppendLine(text);

    private void PushIndent() => _indent++;
    private void PopIndent()  => _indent = Math.Max(0, _indent - 1);
}

namespace IB2PG;

// ── Base types ────────────────────────────────────────────────────────────────

public abstract class AstNode { }

public abstract class StatementNode : AstNode { }

public abstract class ExpressionNode : AstNode { }

// ── Declarations ──────────────────────────────────────────────────────────────

public sealed class DataType : AstNode
{
    public string TypeName  { get; init; } = "";
    public int?   Length    { get; init; }
    public int?   Precision { get; init; }
    public int?   Scale     { get; init; }
}

public sealed class ParameterDecl : AstNode
{
    public string   Name { get; init; } = "";
    public DataType Type { get; init; } = new();
}

public sealed class VariableDecl : AstNode
{
    public string          Name         { get; init; } = "";
    public DataType        Type         { get; init; } = new();
    public ExpressionNode? DefaultValue { get; init; }
}

// ── Top-level node ────────────────────────────────────────────────────────────

public sealed class ProcedureDefinition : AstNode
{
    public string              Name             { get; init; } = "";
    public List<ParameterDecl> InputParameters  { get; init; } = [];
    public List<ParameterDecl> OutputParameters { get; init; } = [];
    public List<VariableDecl>  LocalVariables   { get; init; } = [];
    public List<StatementNode> Body             { get; init; } = [];
    public bool                HasSuspend       { get; init; }
}

// ── Statement nodes ───────────────────────────────────────────────────────────

public sealed class IfStatement : StatementNode
{
    public ExpressionNode       Condition { get; init; } = null!;
    public List<StatementNode>  ThenBody  { get; init; } = [];
    public List<StatementNode>? ElseBody  { get; init; }
}

public sealed class ForSelectLoop : StatementNode
{
    public List<string>        SelectColumns { get; init; } = [];
    public string              FromClause    { get; init; } = "";
    public string?             WhereClause   { get; init; }
    public string?             OtherClauses  { get; init; }
    public List<string>        IntoVariables { get; init; } = [];
    public List<StatementNode> Body          { get; init; } = [];
}

public sealed class WhileStatement : StatementNode
{
    public ExpressionNode      Condition { get; init; } = null!;
    public List<StatementNode> Body      { get; init; } = [];
}

public sealed class SelectIntoStatement : StatementNode
{
    public List<string> SelectColumns { get; init; } = [];
    public List<string> IntoVariables { get; init; } = [];
    public string       FromClause    { get; init; } = "";
    public string?      WhereClause   { get; init; }
    public string?      OtherClauses  { get; init; }
}

public sealed class AssignmentStatement : StatementNode
{
    public string         VariableName { get; init; } = "";
    public ExpressionNode Value        { get; init; } = null!;
}

public sealed class ExecuteProcedureStatement : StatementNode
{
    public string               ProcedureName { get; init; } = "";
    public List<ExpressionNode> Arguments     { get; init; } = [];
    public List<string>?        IntoVariables { get; init; }
}

public sealed class SuspendStatement   : StatementNode { }
public sealed class ExitStatement      : StatementNode { }

public sealed class ExceptionStatement : StatementNode
{
    public string ExceptionName { get; init; } = "";
}

public sealed class RawStatement : StatementNode
{
    public string RawSql { get; init; } = "";
}

// ── Expression nodes ──────────────────────────────────────────────────────────

public sealed class LiteralExpression : ExpressionNode
{
    public string RawValue { get; init; } = "";
}

public sealed class VariableExpression : ExpressionNode
{
    public string Name { get; init; } = "";   // colon already stripped, uppercased
}

public sealed class BinaryExpression : ExpressionNode
{
    public ExpressionNode Left     { get; init; } = null!;
    public string         Operator { get; init; } = "";
    public ExpressionNode Right    { get; init; } = null!;
}

public sealed class UnaryExpression : ExpressionNode
{
    public string         Operator { get; init; } = "";
    public ExpressionNode Operand  { get; init; } = null!;
}

public sealed class FunctionCallExpression : ExpressionNode
{
    public string               FunctionName { get; init; } = "";
    public List<ExpressionNode> Arguments    { get; init; } = [];
}

public sealed class RawExpression : ExpressionNode
{
    public string RawText { get; init; } = "";
}

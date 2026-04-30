# IB2PG Implementation Plan

## Context

The project (`d:\source\Le5\IB2PG`) is a C# console application that converts InterBase stored procedures to PostgreSQL PL/pgSQL functions. The codebase is currently empty — only `CLAUDE.md` (spec) and a stub `readme.md` exist. This plan covers the full implementation of the conversion engine from a blank project to a working, tested translator.

---

## 1. Architecture — Pipeline Flow

```
Input File (.sql)
       |
       v
  FileReader           — load, detect encoding, normalize CRLF→LF, strip BOM
       |
       v
  Tokenizer            — produce List<Token> (no whitespace, no comments)
       |
       v
  InterbaseParser      — recursive descent → List<ProcedureDefinition> (AST)
       |
       v
  PostgresGenerator    — walk AST, emit indented PL/pgSQL text
       |
       v
  Output File (.sql)
```

`Program.cs` owns the pipeline. All other classes are called from it in sequence. Error messages are written to stderr; exit code 1 on failure.

---

## 2. Tokenizer Design

### Token Types (`TokenType` enum)

```
Identifier, QuotedIdentifier, StringLiteral, IntegerLiteral, FloatLiteral
ColonIdent          -- :varname already combined as one token (colon stripped in Value)
LeftParen, RightParen, Comma, Semicolon, Dot
Equals, NotEquals, LessThan, GreaterThan, LessEqual, GreaterEqual
Plus, Minus, Star, Slash, Concatenate
KwCreate, KwProcedure, KwReturns, KwAs, KwBegin, KwEnd
KwDeclare, KwVariable, KwIf, KwThen, KwElse
KwFor, KwSelect, KwFrom, KwWhere, KwInto, KwDo
KwSuspend, KwExecute, KwWhile, KwExit, KwException, KwWhen
KwNot, KwAnd, KwOr, KwNull, KwIs, KwIn, KwLike
KwInsert, KwUpdate, KwDelete, KwSet, KwValues
KwInteger, KwVarchar, KwChar, KwDate, KwTimestamp, KwFloat, KwDouble,
KwPrecision, KwNumeric, KwDecimal, KwSmallint, KwBigint, KwBlob, KwBoolean
KwJoin, KwInner, KwLeft, KwRight, KwOuter, KwOn, KwOrder, KwBy, KwGroup, KwHaving
KwDistinct, KwAll
EndOfFile, Unknown
```

### Token Record

```csharp
public sealed record Token(TokenType Type, string RawText, string Value, int Line, int Column);
```
- `Value` = uppercased for identifiers/keywords, colon-stripped for `ColonIdent`, raw for literals.

### Key Tokenization Rules

- `:varname` → single `ColonIdent` token; `Value = "varname"` (colon stripped here, not later)
- `--` comment → consumed and discarded (no token emitted)
- `/* */` block comment → consumed and discarded
- String literals: `'it''s'` (doubled single-quote escape)
- Quoted identifiers: `"My Field"` → `QuotedIdentifier` with `Value = "My Field"`
- Keyword lookup: static `Dictionary<string, TokenType>` keyed by uppercased text
- `Tokenize()` returns only non-whitespace, non-comment tokens plus `EndOfFile` sentinel

### Tokenizer Class (`Tokenizer.cs`)

```
Tokenizer(string source)
Tokenize() → List<Token>
ReadNext(), ReadIdentifierOrKeyword(), ReadStringLiteral()
ReadQuotedIdentifier(), ReadNumber(), ReadColonOrColonIdent()
SkipLineComment(), SkipBlockComment(), SkipWhitespace()
Current, Peek(offset), Advance(count)
```

---

## 3. Syntax Model (AST) — `SyntaxModel.cs`

All AST types are pure data (no logic). One file, grouped by category.

### Base Types

```csharp
abstract class AstNode { }
abstract class StatementNode : AstNode { }
abstract class ExpressionNode : AstNode { }
```

### Top-Level Node

```csharp
class ProcedureDefinition : AstNode
{
    string Name
    List<ParameterDecl> InputParameters
    List<ParameterDecl> OutputParameters   // from RETURNS (...)
    List<VariableDecl>  LocalVariables     // from DECLARE VARIABLE
    List<StatementNode> Body
    bool HasSuspend                        // drives RETURNS TABLE vs void/scalar
}
```

### Declarations

```csharp
class ParameterDecl : AstNode  { string Name; DataType Type }
class VariableDecl  : AstNode  { string Name; DataType Type; ExpressionNode? Default }
class DataType      : AstNode  { string TypeName; int? Length; int? Precision; int? Scale }
```

### Statement Nodes

| Class | Key Fields |
|---|---|
| `IfStatement` | `Condition`, `ThenBody`, `ElseBody?` |
| `ForSelectLoop` | `SelectColumns`, `FromClause`, `WhereClause?`, `IntoVariables`, `Body` |
| `WhileStatement` | `Condition`, `Body` |
| `SelectIntoStatement` | `SelectColumns`, `IntoVariables`, `FromClause`, `OtherClauses?` |
| `AssignmentStatement` | `VariableName`, `Value` (ExpressionNode) |
| `ExecuteProcedureStatement` | `ProcedureName`, `Arguments`, `IntoVariables?` |
| `SuspendStatement` | (empty) |
| `ExitStatement` | (empty) |
| `ExceptionStatement` | `ExceptionName` |
| `RawStatement` | `RawSql` (pass-through fallback) |

### Expression Nodes

| Class | Key Fields |
|---|---|
| `LiteralExpression` | `RawValue` (string/int/float/NULL) |
| `VariableExpression` | `Name` (colon already stripped) |
| `BinaryExpression` | `Left`, `Operator`, `Right` |
| `UnaryExpression` | `Operator`, `Operand` |
| `FunctionCallExpression` | `FunctionName`, `Arguments` |
| `RawExpression` | `RawText` (complex expressions not fully modeled) |

---

## 4. InterBase Parser Design — `InterbaseParser.cs`

Recursive descent. One class, all private helpers.

### Core Invariant

**Each `ParseXxx` that opens a `BEGIN` also consumes its matching `END`.** The recursive call stack is the nesting tracker — no depth counter needed.

### Key Methods

```
ParseAll()                  → List<ProcedureDefinition>
ParseProcedure()            → ProcedureDefinition
ParseParameterList()        → List<ParameterDecl>
ParseReturnsClause()        → List<ParameterDecl>
ParseDeclareBlock()         → List<VariableDecl>
ParseDataType()             → DataType

ParseStatementList()        → List<StatementNode>   // loops until KwEnd or EOF
ParseStatement()            → StatementNode          // dispatcher
ParseIf()                   → IfStatement
ParseForSelect()            → ForSelectLoop
ParseWhile()                → WhileStatement
ParseSelectInto()           → SelectIntoStatement
ParseAssignment()           → AssignmentStatement
ParseExecuteProcedure()     → ExecuteProcedureStatement
ParseSuspend()              → SuspendStatement
ParseRawStatement()         → RawStatement           // fallback: collect to semicolon

ParseExpression()           → ExpressionNode         // full precedence chain
ParseOrExpression(), ParseAndExpression(), ParseNotExpression()
ParseComparison(), ParseAddSub(), ParseMulDiv()
ParseUnary(), ParsePrimary()

Current, Peek(offset), Consume(), Expect(type), TryConsume(type)
```

### Dispatch Logic in `ParseStatement()`

| Lookahead | Action |
|---|---|
| `KwIf` | `ParseIf()` |
| `KwFor` + `KwSelect` | `ParseForSelect()` |
| `KwWhile` | `ParseWhile()` |
| `KwSelect` (standalone) | `ParseSelectInto()` (degrades to `RawStatement` if no INTO) |
| `ColonIdent` or `Identifier` + `Equals` | `ParseAssignment()` |
| `KwExecute` + `KwProcedure` | `ParseExecuteProcedure()` |
| `KwSuspend` | `ParseSuspend()` |
| `KwExit` | `ParseExit()` |
| anything else | `ParseRawStatement()` |

### FOR SELECT Parsing Steps

1. Consume `FOR`, `SELECT`
2. Collect column tokens as raw strings until `INTO`
3. Consume `INTO`, collect `ColonIdent` tokens → `IntoVariables`
4. Consume `FROM`, collect raw until `DO` → `FromClause`
5. Consume `DO`, `BEGIN` (or just `DO BEGIN`)
6. `ParseStatementList()` → `Body`
7. Consume `END`

### SELECT INTO Parsing Steps

1. Consume `SELECT`, collect columns until `INTO`
2. Consume `INTO`, collect `ColonIdent` → `IntoVariables`
3. Consume `FROM`, collect raw until `;` or clause keyword → `FromClause`
4. Optional `WHERE`, `ORDER BY`, `GROUP BY` → `OtherClauses`
5. Consume `;`

---

## 5. PostgreSQL Generator Design — `PostgresGenerator.cs`

### Strategy: Switch-Based Dispatch with Indentation Stack

```csharp
void EmitStatement(StatementNode stmt) => stmt switch
{
    IfStatement s         => EmitIf(s),
    ForSelectLoop s       => EmitForSelect(s),
    WhileStatement s      => EmitWhile(s),
    SelectIntoStatement s => EmitSelectInto(s),
    AssignmentStatement s => EmitAssignment(s),
    ExecuteProcedureStatement s => EmitExecuteProcedure(s),
    SuspendStatement      => AppendLine("RETURN NEXT;"),
    ExitStatement         => AppendLine("RETURN;"),
    RawStatement s        => EmitRaw(s),
    _ => throw new GeneratorException(...)
};
```

### Procedure Frame Template

```sql
CREATE OR REPLACE FUNCTION {name}({params})
RETURNS {return_type}
LANGUAGE plpgsql
AS $$
DECLARE
    {local_vars}
BEGIN
    {body}
END;
$$;
```

Return type rules:
- `HasSuspend=true` and `OutputParameters.Count > 0` → `RETURNS TABLE(col type, ...)`
- Single output param, no SUSPEND → `RETURNS scalar_type`
- No output params → `RETURNS void`

### Emission Rules

| Source | Output |
|---|---|
| `IF cond THEN...END IF;` | braces and parens stripped from condition |
| `FOR v IN SELECT... LOOP...END LOOP;` | single var or `(v1, v2)` for multiple |
| `SELECT cols INTO var FROM tbl...;` | INTO moved before FROM |
| `EXECUTE PROCEDURE p(...)` with no INTO | `PERFORM p(...);` |
| `EXECUTE PROCEDURE p(...) RETURNING_VALUES :v` | `SELECT v FROM p(...);` |
| `SUSPEND` | `RETURN NEXT;` |
| `:varname` in any context | `varname` (handled at tokenization) |

### Colon Stripping for Raw Strings

`RawTextTransformer.StripColons(string raw)` — state machine (not regex):
- Scans character by character
- When inside a string literal (single-quote tracking), passes through unchanged
- When `:` is followed by a letter or `_`, removes the `:`

---

## 6. Full C# Class List

| File | Class/Type | Role |
|---|---|---|
| `Program.cs` | `Program` | CLI args, pipeline wiring, error output |
| `FileReader.cs` | `FileReader` (static) | Read, normalize, split into procedure blocks |
| `Token.cs` | `Token` (record), `TokenType` (enum) | Token data types |
| `Tokenizer.cs` | `Tokenizer` | Lexical analysis |
| `SyntaxModel.cs` | All AST node classes | Pure data, no logic |
| `InterbaseParser.cs` | `InterbaseParser` | Recursive descent parser |
| `PostgresGenerator.cs` | `PostgresGenerator` | AST → PL/pgSQL text |
| `RawTextTransformer.cs` | `RawTextTransformer` (static) | `StripColons`, `NormalizeKeywordCase` |
| `TypeMapper.cs` | `TypeMapper` (static) | IB type names → PG type names |
| `ParseException.cs` | `ParseException` | Parser errors with line/col |
| `GeneratorException.cs` | `GeneratorException` | Generator errors with AST node ref |

### TypeMapper Examples

| InterBase | PostgreSQL |
|---|---|
| `INTEGER` | `integer` |
| `SMALLINT` | `smallint` |
| `BIGINT` | `bigint` |
| `FLOAT` | `double precision` |
| `DOUBLE PRECISION` | `double precision` |
| `NUMERIC(p,s)` | `numeric(p,s)` |
| `VARCHAR(n)` | `varchar(n)` |
| `DATE` | `date` |
| `TIMESTAMP` | `timestamp` |
| `BLOB` | `text` (with comment warning) |

---

## 7. Step-by-Step Development Phases

### Phase 1 — Project Skeleton
- `dotnet new console -n IB2PG`
- `.csproj` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`
- Create all planned `.cs` files as stubs
- `Program.cs` reads `args[0]`, calls `FileReader.ReadAndNormalize`, prints char count
- **Gate:** `dotnet run input.sql` exits 0

### Phase 2 — Tokenizer
- Full `TokenType` enum
- Full `Token` record
- Full `Tokenizer` class
- Unit tests: keywords, ColonIdent, string literals with `''`, both comment styles, all operators
- **Gate:** Tokenize a complete IB procedure with zero `Unknown` tokens

### Phase 3 — AST Nodes
- All classes in `SyntaxModel.cs` (data only)
- **Gate:** Code compiles; can manually instantiate `ProcedureDefinition` in a test

### Phase 4 — Parser: Procedure Header
- `ParseAll`, `ParseProcedure`, `ParseParameterList`, `ParseReturnsClause`
- `ParseDeclareBlock`, `ParseDataType`
- **Gate:** Parser reads header (no body) → correct `ProcedureDefinition` with params and vars

### Phase 5 — Parser: Simple Statements
- `ParseStatementList` dispatcher
- `ParseAssignment`, `ParseSuspend`, `ParseRawStatement`
- Full expression parser (`ParseExpression` → `ParsePrimary`)
- **Gate:** Procedures with assignments and SUSPEND produce correct AST

### Phase 6 — Parser: Control Flow
- `ParseIf` (with ELSE), `ParseForSelect`, `ParseWhile`
- `ParseSelectInto`, `ParseExecuteProcedure`
- **Gate:** Nested IF inside FOR inside IF → correct nested AST

### Phase 7 — Generator: Procedure Frame
- `EmitProcedure`, `EmitSignature`, `EmitReturnType`, `EmitDeclareBlock`
- `TypeMapper` fully implemented
- **Gate:** Procedure with empty body emits correct PG function skeleton

### Phase 8 — Generator: All Statements
- All `EmitXxx` methods
- `EmitExpression`, `RawTextTransformer.StripColons`
- **Gate:** Full procedure with all statement types emits syntactically valid PL/pgSQL

### Phase 9 — End-to-End Integration
- `FileReader.SplitIntoProcedureBlocks` (handle multi-procedure files, strip `SET TERM`)
- `--output` / `-o` CLI flag
- `--verbose` flag (dump tokens + AST to stderr)
- Graceful `ParseException` reporting with line/col
- **Gate:** Real IB SQL dump with 5+ procedures converts without crash; output validates in psql

### Phase 10 — Edge Cases
- Empty `BEGIN END` → `NULL; -- empty block`
- Single-statement IF (no BEGIN/END)
- FOR SELECT with no WHERE
- Multiple INTO variables (3+)
- Procedures with zero input params
- BLOB type with warning comment
- **Gate:** All edge case test inputs convert correctly

---

## 8. Testing Strategy

### Test Project

Separate `IB2PG.Tests` xUnit project. Files:
- `TokenizerTests.cs`
- `ParserTests.cs`
- `GeneratorTests.cs`
- `IntegrationTests.cs` — pairs of `TestData/input_NNN.sql` + `expected_NNN.sql`

### Critical Test Cases

| Category | Test |
|---|---|
| Tokenizer | ColonIdent strips colon; `''` escape in string literal; `--` discarded; `/* */` discarded |
| Parser | Nested IF in FOR produces correct recursive AST |
| Parser | SELECT INTO: INTO moves before FROM |
| Parser | EXECUTE PROCEDURE with/without RETURNING_VALUES |
| Generator | `HasSuspend=true` → `RETURNS TABLE(...)` |
| Generator | Colon variables stripped in raw clauses |
| Generator | Indentation is correct at each nesting level |
| Integration | INT_001–INT_010 covering all syntax patterns (see plan) |
| Error | `ParseException` with correct line/col for missing END |

---

## Critical Files to Create

All files are new (project has no existing implementation):

- `d:\source\Le5\IB2PG\IB2PG.csproj`
- `d:\source\Le5\IB2PG\Program.cs`
- `d:\source\Le5\IB2PG\FileReader.cs`
- `d:\source\Le5\IB2PG\Token.cs` (Token record + TokenType enum)
- `d:\source\Le5\IB2PG\Tokenizer.cs`
- `d:\source\Le5\IB2PG\SyntaxModel.cs` (all AST nodes)
- `d:\source\Le5\IB2PG\InterbaseParser.cs`
- `d:\source\Le5\IB2PG\PostgresGenerator.cs`
- `d:\source\Le5\IB2PG\RawTextTransformer.cs`
- `d:\source\Le5\IB2PG\TypeMapper.cs`
- `d:\source\Le5\IB2PG\ParseException.cs`
- `d:\source\Le5\IB2PG\GeneratorException.cs`
- `d:\source\Le5\IB2PG.Tests\IB2PG.Tests.csproj`
- `d:\source\Le5\IB2PG.Tests\TokenizerTests.cs`
- `d:\source\Le5\IB2PG.Tests\ParserTests.cs`
- `d:\source\Le5\IB2PG.Tests\GeneratorTests.cs`
- `d:\source\Le5\IB2PG.Tests\IntegrationTests.cs`
- `d:\source\Le5\IB2PG.Tests\TestData\` (IB+PG SQL pairs)

## Verification

End-to-end test: run `dotnet run -- samples/sample.sql --output out.sql` on a real IB procedure file and validate `out.sql` is accepted by `psql -f out.sql` against a PostgreSQL instance (or passes `pg_dump --schema-only` syntax check).

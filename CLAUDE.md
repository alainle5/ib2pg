# CLAUDE.md

## 1. Project Overview

This project is a C# console application that converts InterBase stored
procedures into PostgreSQL functions written in PL/pgSQL.

**Input:** A text file containing an InterBase stored procedure.

**Output:** A file containing the equivalent PostgreSQL function.

The converter must be a deterministic SQL translator. Prioritize
semantic correctness and valid PostgreSQL over textual similarity to the
original procedure.

## 2. Instructions for Claude

When working on this project:

1.  Read this file before making changes.
2.  Inspect the existing code before proposing or implementing changes.
3.  Preserve the current architecture and existing functionality unless
    a change is necessary and explained.
4.  Prefer tokenization, parsing, and syntax-aware transformations over
    global regular-expression or text replacements.
5.  Keep parsing, transformation rules, and SQL generation modular.
6.  Do not silently discard unsupported syntax. Report it clearly or
    fail with a useful diagnostic.
7.  Do not claim that code builds, tests pass, or generated SQL is valid
    unless the relevant commands were actually run successfully.
8.  Make focused changes. Do not rewrite the entire converter to
    implement an isolated rule.
9.  Add regression tests for every bug fix or new conversion rule.
10. If an InterBase construct is ambiguous, explain the ambiguity and
    ask for clarification rather than guessing at semantics.

## 3. Required Conversion Pipeline

Use or preserve a pipeline equivalent to:

1.  Read the input file.
2.  Normalize whitespace without changing string literals or comments.
3.  Tokenize the SQL while recognizing strings, quoted identifiers,
    comments, punctuation, and operators.
4.  Parse the procedure into a syntax model.
5.  Apply syntax-aware conversion rules.
6.  Generate PostgreSQL function SQL.
7.  Write the output file.
8.  Run automated tests and, where available, validate generated
    functions against PostgreSQL.

Do not rely on whitespace or line breaks to determine SQL structure.

## 4. Supported InterBase Syntax

### 4.1 IF / THEN / BEGIN / END

InterBase examples:

``` sql
IF (CONDITION) THEN STATEMENT;

IF (CONDITION) THEN
BEGIN
    STATEMENT1;
    STATEMENT2;
END
```

PostgreSQL equivalent:

``` sql
IF condition THEN
    statement;
END IF;
```

Rules:

-   Convert InterBase conditional blocks into valid PL/pgSQL
    `IF ... THEN ... END IF;` blocks.
-   Handle nested IF blocks.
-   Preserve statement order and logic.
-   Remove parentheses around a condition only when doing so preserves
    the expression's meaning.
-   Do not mistake `END` closing an IF block for the end of the entire
    function.
-   Ensure statements and block terminators have valid PostgreSQL
    syntax.

### 4.2 SELECT INTO

InterBase:

``` sql
SELECT FIELD1, FIELD2
FROM SOME_TABLE
WHERE CONDITION
INTO :VAR1, :VAR2;
```

PostgreSQL:

``` sql
SELECT field1, field2
INTO var1, var2
FROM some_table
WHERE condition;
```

Rules:

-   Move `INTO` to PostgreSQL's position between the select list and
    `FROM`.
-   Remove the colon prefix from procedural variables.
-   Preserve the select list, predicates, joins, ordering, and other
    clauses.
-   Do not confuse a procedural variable with a database column.
-   Handle multiple target variables.
-   Do not apply this transformation blindly to SQL statements where
    `INTO` has a different meaning.

### 4.3 FOR SELECT DO BEGIN

InterBase:

``` sql
FOR SELECT FIELD
    FROM SOME_TABLE
    INTO :VAR
DO
BEGIN
    STATEMENT;
END
```

Typical PostgreSQL equivalent:

``` sql
FOR var IN
    SELECT field
    FROM some_table
LOOP
    statement;
END LOOP;
```

Rules:

-   Convert `FOR SELECT ... DO BEGIN` into a valid PL/pgSQL
    `FOR ... IN SELECT ... LOOP` construct.
-   Convert the corresponding closing `END` into `END LOOP;`.
-   Remove colon prefixes from procedural variables.
-   Preserve query filters, joins, ordering, and loop body semantics.
-   Support loops nested inside IF blocks and IF blocks nested inside
    loops.
-   Determine the correct loop target and select-list mapping from the
    parsed syntax; do not assume every loop selects a single field.

### 4.4 Variable Prefixes

InterBase uses a colon prefix for procedural variables and parameters in
SQL expressions, for example:

``` sql
:TOTAL
:LIMIT
```

In generated PL/pgSQL expressions, remove the InterBase colon prefix:

``` sql
total
limit
```

Rules:

-   Remove the colon only when it identifies an InterBase procedural
    variable or parameter.
-   Do not alter colons inside string literals, comments, casts, or
    unrelated syntax.
-   Normalize generated unquoted variable and parameter names to
    lowercase consistently.
-   Do not quote a procedural variable just because its name matches a
    database field or SQL keyword.
-   Distinguish procedural variables and parameters from database column
    references using parsing context and the syntax model.

### 4.5 DECLARE VARIABLE

InterBase:

``` sql
DECLARE VARIABLE TOTAL INTEGER;
```

PostgreSQL declaration:

``` sql
DECLARE
    total integer;
```

Rules:

-   Place local variable declarations in the PL/pgSQL `DECLARE` section
    before `BEGIN`.
-   Convert supported InterBase data types to suitable PostgreSQL types.
-   Normalize unquoted variable names to lowercase.
-   Preserve declaration order where practical.
-   Do not treat input parameters or output fields as local variables.

### 4.6 SUSPEND

InterBase:

``` sql
SUSPEND;
```

Typical PostgreSQL equivalent for a set-returning PL/pgSQL function:

``` sql
RETURN NEXT;
```

Rules:

-   Convert `SUSPEND` according to the generated function's return
    model.
-   Confirm that the function's return type and output variables make
    `RETURN NEXT` valid.
-   Do not apply this transformation without checking the function
    return semantics.

### 4.7 EXECUTE PROCEDURE

InterBase:

``` sql
EXECUTE PROCEDURE PROC_NAME(ARG1, ARG2);
```

Potential PostgreSQL equivalents include:

``` sql
PERFORM proc_name(arg1, arg2);
```

or, when a returned value or row is required, an appropriate
`SELECT ... INTO`, `SELECT`, or other valid invocation.

Rules:

-   Determine whether the called routine is a PostgreSQL function or
    procedure and whether its result is used.
-   Use `PERFORM` when the function result is intentionally ignored.
-   Capture or return results when required by the source procedure's
    semantics.
-   Do not assume every InterBase procedure call can be converted to
    `PERFORM`.

## 5. Procedure Definition Conversion

InterBase example:

``` sql
CREATE PROCEDURE PROC_NAME (PARAM1 INTEGER)
RETURNS (RESULT INTEGER)
AS
BEGIN
    ...
END
```

Typical PostgreSQL function structure:

``` sql
CREATE OR REPLACE FUNCTION proc_name(param1 integer)
RETURNS TABLE (result integer)
LANGUAGE plpgsql
AS $$
BEGIN
    ...
END;
$$;
```

Rules:

-   Convert the procedure to a PostgreSQL function when appropriate for
    the source procedure's behavior.
-   Convert InterBase output parameters to a suitable PostgreSQL return
    type, such as `RETURNS TABLE`, when semantically correct.
-   Place local variables in a `DECLARE` section.
-   Use `LANGUAGE plpgsql` and valid dollar-quoted function bodies.
-   Generate correct statement terminators and block endings.
-   Account for procedures that return no rows, one value, or multiple
    rows.
-   Do not assume `RETURNS TABLE` is correct for every InterBase
    procedure; select the PostgreSQL return model based on the source
    semantics.
## String Literal Conversion — InterBase to PostgreSQL

### Conversion Rules

InterBase source may contain double-quoted values that are intended to represent string literals. When the parser identifies a double-quoted expression as a string value, convert it to a PostgreSQL single-quoted string literal before applying reserved-identifier quoting rules.

Examples:

**Example 1 — String value**

InterBase:
```sql
IF (STATUS = "ACTIVE") THEN
```

PostgreSQL:
```sql
IF status = 'ACTIVE' THEN
```

**Example 2 — Value containing a reserved word**

InterBase:
```sql
IF (DESCRIPTION = "LIMIT") THEN
```

PostgreSQL:
```sql
IF description = 'LIMIT' THEN
```

The word `LIMIT` inside a string literal must remain a string value. It must not be converted to the identifier `"limit"`.

**Example 3 — Reserved database field**

InterBase:
```sql
SELECT LIMIT FROM POLICY
WHERE DESCRIPTION = "LIMIT";
```

PostgreSQL:
```sql
SELECT "limit" FROM policy
WHERE description = 'LIMIT';
```

### Implementation Requirements

1. Identify whether a double-quoted expression represents a string value or a delimited identifier before converting it.
2. Convert confirmed string values from double quotes to single quotes.
3. Escape embedded single quotes correctly when generating PostgreSQL string literals.
4. Preserve the contents of string values, including reserved words such as `LIMIT`.
5. Do not convert double-quoted identifiers into string literals.
6. Perform string-literal conversion before applying reserved-identifier rules to the parsed syntax model.
7. Do not use global quote replacement. Use tokenization and parsing to preserve SQL semantics.
8. Add regression tests for string values, reserved words inside strings, embedded quotes, and quoted identifiers.
9. If the source syntax is ambiguous, report the ambiguity rather than silently changing the meaning.



## 6. Identifier Case and Reserved Field Names

### 6.1 General Identifier Rules

-   PostgreSQL folds unquoted identifiers to lowercase.
-   Generate ordinary unquoted identifiers in lowercase unless the
    source semantics require otherwise.
-   Use double quotes only when required to preserve or disambiguate an
    identifier.
-   Do not change string literals, comments, or identifier text embedded
    in literals.
-   Preserve the identity of database objects while applying consistent
    naming rules.

### 6.2 Special Rule for the Database Field LIMIT

The InterBase database field named `LIMIT` must be generated as the
lowercase, double-quoted PostgreSQL identifier `"limit"`.

The converter must distinguish this database field from a procedural
parameter or variable named `LIMIT`, and from PostgreSQL's `LIMIT` SQL
keyword.

Rules:

1.  **Unqualified database column:** `LIMIT` becomes `"limit"` when the
    parser identifies it as a database column reference.
2.  **Qualified database column:** `P.LIMIT` becomes `p."limit"`.
3.  **Procedural parameter or variable:** `:LIMIT` becomes `limit`,
    without double quotes.
4.  **Parameter declaration and references:** Use the same lowercase,
    unquoted parameter name consistently throughout the generated
    function.
5.  **SQL keyword:** Preserve `LIMIT` as a keyword in a row-limiting
    clause, for example `SELECT name FROM policy LIMIT 10`.
6.  **Other identifiers:** Do not treat `LIMIT_AMOUNT` or `MY_LIMIT` as
    the field `LIMIT`. Apply the general identifier naming rules to
    these identifiers.
7.  **String literals and comments:** Do not change occurrences of
    `LIMIT` inside string literals or comments.
8.  **Context matters:** Do not quote every occurrence of the text
    `LIMIT`. Use tokenizer/parser information and the syntax model to
    determine whether it is a column, parameter, variable, or SQL
    keyword.
9.  **All relevant SQL contexts:** Apply the rule to database column
    references in SELECT lists, WHERE predicates, JOIN conditions, ORDER
    BY, GROUP BY, UPDATE expressions, and other applicable SQL
    expressions.
10. **Do not break semantics:** If the parser cannot determine whether
    `LIMIT` is a column or keyword in a particular construct, report the
    ambiguity rather than silently producing potentially incorrect SQL.

Examples:

InterBase:

``` sql
SELECT LIMIT FROM POLICY;
```

PostgreSQL:

``` sql
SELECT "limit" FROM policy;
```

InterBase:

``` sql
SELECT P.LIMIT
FROM POLICY P
WHERE P.LIMIT > :LIMIT;
```

PostgreSQL:

``` sql
SELECT p."limit"
FROM policy p
WHERE p."limit" > limit;
```

InterBase:

``` sql
IF (:LIMIT > 0) THEN
    TOTAL = :LIMIT;
```

PostgreSQL:

``` sql
IF limit > 0 THEN
    total := limit;
END IF;
```

InterBase:

``` sql
SELECT NAME FROM POLICY LIMIT 10;
```

PostgreSQL:

``` sql
SELECT name FROM policy LIMIT 10;
```

Implementation requirements:

-   Implement this as a syntax-aware identifier rule, preferably in
    `SyntaxRules.cs` or the existing equivalent.
-   Use the parsed syntax model to distinguish database columns from
    variables and SQL keywords.
-   Do not implement this rule using global string replacement.
-   Add regression tests for qualified and unqualified columns,
    procedural parameters, SQL row-limiting clauses, string literals,
    comments, and similarly named identifiers.

## 7. Parsing Strategy

Avoid naive global regex replacements for syntax-sensitive
transformations.

Prefer:

1.  A tokenizer that recognizes identifiers, punctuation, operators,
    strings, quoted identifiers, and comments.
2.  A parser that tracks nested `BEGIN` / `END`, IF blocks, loops,
    declarations, and SQL statements.
3.  An intermediate syntax model.
4.  Syntax-aware transformation rules.
5.  A PostgreSQL generator that emits valid PL/pgSQL.

If the existing implementation uses a different architecture, inspect it
first and recommend a minimal, safe evolution rather than rewriting it
automatically.

## 8. Suggested Internal Syntax Model

A procedure model may include:

-   `ProcedureDefinition`
    -   Name
    -   Parameters
    -   Return fields / return model
    -   Local variables
    -   Statements

Statement nodes may include:

-   `IfStatement`
-   `ForSelectLoop`
-   `SelectIntoStatement`
-   `AssignmentStatement`
-   `ExecuteProcedureStatement`
-   Other statement types required by supported input syntax

Identifier references should preserve enough context to distinguish:

-   Database columns
-   Qualified database columns
-   Procedural parameters
-   Local variables
-   SQL keywords
-   String literals and comments

## 9. Suggested C# Application Structure

Use the existing project structure if it already provides equivalent
responsibilities. A possible organization is:

-   `Program.cs` --- Accept input/output paths and run the pipeline.
-   `FileReader.cs` --- Read source files and perform safe
    normalization.
-   `InterbaseParser.cs` --- Tokenize and parse InterBase syntax.
-   `SyntaxModel.cs` --- Represent procedures, declarations,
    expressions, and statements.
-   `PostgresGenerator.cs` --- Generate PostgreSQL function SQL.
-   `SyntaxRules.cs` --- Centralize syntax and identifier transformation
    rules.
-   Test project --- Automated parser, conversion, and regression tests.

Do not create duplicate classes or files if equivalent components
already exist.

## 10. File Processing Pipeline

``` text
Read source file
    ↓
Tokenize and safely normalize
    ↓
Parse InterBase syntax
    ↓
Build syntax model
    ↓
Apply conversion rules
    ↓
Generate PostgreSQL function
    ↓
Write output file
    ↓
Run regression tests and PostgreSQL validation when available
```

## 11. Edge Cases

The converter should support and test:

-   Nested IF blocks.
-   IF blocks containing loops.
-   Loops containing IF blocks.
-   Multiple statements inside a block.
-   Multiple `SELECT INTO` targets.
-   Empty `BEGIN ... END` blocks, where valid.
-   Multiple output columns.
-   Variables and parameters that share names with database columns.
-   Qualified and unqualified column references.
-   The special database field `LIMIT`.
-   String literals and comments containing SQL keywords.
-   Identifiers such as `LIMIT_AMOUNT`.
-   SQL row-limiting clauses using `LIMIT`.
-   Procedures that return multiple rows.
-   Unsupported or ambiguous constructs, with clear diagnostics.

## 12. Testing Strategy

Create automated tests for each supported conversion rule.

Minimum tests:

1.  Simple IF.
2.  IF with BEGIN / END.
3.  Nested IF.
4.  SELECT INTO with one target.
5.  SELECT INTO with multiple targets.
6.  FOR SELECT loops.
7.  Nested loops and IF blocks.
8.  DECLARE VARIABLE.
9.  SUSPEND and function return behavior.
10. EXECUTE PROCEDURE with ignored and used results.
11. Procedure signature and return conversion.
12. Unqualified database column `LIMIT` becomes `"limit"`.
13. Qualified database column `P.LIMIT` becomes `p."limit"`.
14. Procedural parameter `:LIMIT` becomes `limit`, without quotes.
15. SQL `LIMIT 10` remains a keyword.
16. String literals and comments containing `LIMIT` remain unchanged.
17. `LIMIT_AMOUNT` is not mistaken for `LIMIT`.
18. Generated SQL compiles against a PostgreSQL test database when one
    is available.

For each test, compare the actual generated SQL to the expected SQL.
Where formatting is not semantically significant, use a suitable
normalized comparison, while retaining tests that verify required
quoting and statement structure.

A successful C# build does not prove the generated PostgreSQL is valid.
Report separately whether the converter builds, automated tests pass,
and generated functions compile against PostgreSQL.

## 13. Coding Guidelines

-   Use C# and follow the existing project's target framework and
    conventions.
-   Prefer structured parsing over global regex replacement.
-   Keep parsing, transformation, and generation responsibilities
    separate.
-   Preserve statement order and business logic.
-   Preserve indentation where practical.
-   Provide clear error messages for unsupported syntax.
-   Avoid unrelated refactoring.
-   Do not report tests as passing unless they were actually run.
-   Document any conversion limitations.

## 14. Workflow for Claude Code

Use the following prompts in order. Do not skip inspection and testing.

### Prompt 1 --- Inspect the Existing Project

Read `CLAUDE.md` and inspect the current converter. Do not change files
yet.

Identify: - The project structure and target framework. - How
tokenization and parsing currently work. - How variables, parameters,
and column identifiers are represented. - Where SQL conversion rules are
implemented. - Existing tests and how to run them. - Gaps between the
implementation and this specification.

Pay particular attention to how the converter can distinguish the
database column `LIMIT` from a procedural parameter `:LIMIT` and the SQL
keyword `LIMIT`.

Recommend the smallest safe implementation plan. Identify the exact
files that need changes. Do not rewrite the application.

### Prompt 2 --- Implement the Conversion Changes

Using your inspection findings, implement the required conversion rules
in the existing application.

Requirements: - Preserve the existing architecture and working
functionality. - Implement the `LIMIT` identifier rule: database column
`LIMIT` becomes `"limit"`; qualified `P.LIMIT` becomes `p."limit"`;
procedural `:LIMIT` becomes `limit`; SQL `LIMIT 10` remains a keyword. -
Use syntax-aware parsing, not global text replacement. - Implement only
changes supported by the parsed context. - Add or update automated
regression tests. - Do not claim the work is complete until the tests
have been run.

After the changes, summarize the files modified and explain the
implementation.

### Prompt 3 --- Test and Fix Regressions

Run the existing test suite and all new regression tests.

Verify at minimum: - Database column `LIMIT` is quoted as `"limit"`. -
Qualified column `P.LIMIT` is converted to `p."limit"`. - Procedural
parameter `:LIMIT` becomes `limit` without quotes. - SQL `LIMIT 10`
remains unchanged as a keyword. - `LIMIT_AMOUNT`, string literals, and
comments are not incorrectly modified. - Existing IF, SELECT INTO, FOR
SELECT, variable, SUSPEND, and EXECUTE PROCEDURE conversions still
behave as expected.

Fix defects introduced by the change and rerun the tests. Report the
exact commands executed and their actual results. If any test cannot be
run, explain why; do not report it as passing.

### Prompt 4 --- Build and Validate the Converter

Build the existing C# solution or project using its current
configuration.

Then: 1. Run all automated tests. 2. Convert representative InterBase
stored procedures that exercise the changed rules and existing syntax
features. 3. Inspect the generated PostgreSQL SQL for correctness. 4. If
a PostgreSQL test database is available, compile the generated functions
there. 5. Report build status, test results, sample conversion results,
and remaining limitations.

Do not regenerate the entire application from scratch. Modify the
existing code, build it, and validate the changes. If a build or test
fails, diagnose and fix the problem where possible, then rerun the
relevant commands.

## 15. Future Enhancements

Possible future improvements:

-   ANTLR grammar for InterBase SQL.
-   More complete SQL tokenizer and parser.
-   Automated PostgreSQL validation.
-   Dependency tracking between procedures.
-   A configurable reserved-identifier mapping.
-   Batch conversion with per-file diagnostics and a summary report.

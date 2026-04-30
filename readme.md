# Interbase to Postgres
## Claude first command
> Read the CLAUDE.md file in this project and create a detailed implementation plan.

The plan should include:
1. Architecture of the parser
2. Syntax model structures
3. InterBase tokenization strategy
4. PostgreSQL generation strategy
5. C# class design
6. Step-by-step development phases

Do not write full code yet. Focus on planning the conversion engine.

## Claude second command
You are NOT allowed to implement a regex-based converter.

Your task is to design a production-grade parsing architecture for converting InterBase stored procedures into PostgreSQL PL/pgSQL functions.

Before writing any code, do the following:

1. Analyze InterBase stored procedure syntax (IF/THEN, BEGIN/END blocks, SELECT INTO, FOR SELECT loops, SUSPEND, EXECUTE PROCEDURE).
2. Identify all structural patterns and nested constructs.
3. Design a formal parsing strategy using a tokenizer + parser (not line-based parsing).
4. Define a complete Abstract Syntax Tree (AST) model in C# that represents:
   - procedures
   - statements
   - expressions
   - control flow (IF, FOR, loops)
5. Define transformation rules from AST → PostgreSQL PL/pgSQL.
6. Define how variable scoping and :parameter conversion will be handled.
7. Define how block nesting (BEGIN/END) is tracked safely.
8. Provide a step-by-step architecture for the C# solution.

Constraints:
- DO NOT use regex as the primary mechanism for parsing.
- DO NOT generate conversion code yet.
- DO NOT simplify SQL into string replacements.
- Assume the input contains deeply nested and messy legacy InterBase SQL.

Output format:
- Architecture overview
- Tokenizer design
- Parser design
- AST class design (C#)
- Transformation pipeline
- Edge cases & how they are handled
- Implementation phases
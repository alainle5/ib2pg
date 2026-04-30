# CLAUDE.md

## Project Overview

This project is a C# console application that converts InterBase stored
procedures into PostgreSQL functions.

Input: A text file containing a stored procedure written in InterBase
SQL syntax.

Output: A new file containing an equivalent PostgreSQL function written
in PL/pgSQL.

The converter must parse InterBase syntax and transform it into valid
PostgreSQL syntax while preserving logic.

------------------------------------------------------------------------

# Responsibilities of Claude

Claude must assist with implementing logic that:

1.  Reads a text file containing an InterBase stored procedure.
2.  Parses the procedure syntax.
3.  Converts it to PostgreSQL PL/pgSQL syntax.
4.  Writes the converted function to an output file.

Claude should behave as a **deterministic SQL translator**, prioritizing
semantic correctness over textual similarity.

------------------------------------------------------------------------

# Key InterBase Syntax That Must Be Supported

## IF THEN

InterBase

IF (condition) THEN statement;

or

IF (condition) THEN BEGIN statements END

PostgreSQL

IF condition THEN statements; END IF;

Rules: - Remove parentheses around conditions - Replace BEGIN/END with
PostgreSQL block structure - Always terminate IF blocks with END IF

------------------------------------------------------------------------

## SELECT INTO

InterBase

SELECT field1, field2 FROM table WHERE condition INTO :var1, :var2;

PostgreSQL

SELECT field1, field2 INTO var1, var2 FROM table WHERE condition;

Rules: - Move INTO before FROM - Remove colon prefixes

------------------------------------------------------------------------

## FOR SELECT DO BEGIN

InterBase

FOR SELECT field FROM table INTO :var DO BEGIN statements END

PostgreSQL

FOR var IN SELECT field FROM table LOOP statements; END LOOP;

Rules: - Convert DO BEGIN to LOOP - Convert END to END LOOP - Remove
colon prefixes

------------------------------------------------------------------------

## Variable Prefix

InterBase variables use :

Example

:value

PostgreSQL

value

Rule: Remove colon prefixes from variables.

------------------------------------------------------------------------

## DECLARE VARIABLE

InterBase

DECLARE VARIABLE TOTAL INTEGER;

PostgreSQL

DECLARE total integer;

Rules: - Move variable declarations inside DECLARE block - Lowercase
names where possible

------------------------------------------------------------------------

## SUSPEND

InterBase procedures often use SUSPEND to emit rows.

Example

SUSPEND;

PostgreSQL equivalent

RETURN NEXT;

Rule: Convert SUSPEND to RETURN NEXT.

------------------------------------------------------------------------

## EXECUTE PROCEDURE

InterBase

EXECUTE PROCEDURE PROC_NAME(...);

PostgreSQL

PERFORM proc_name(...);

or

SELECT \* FROM proc_name(...);

Rule: Choose SELECT if results are expected.

------------------------------------------------------------------------

# Procedure Definition Conversion

InterBase

CREATE PROCEDURE PROC_NAME ( PARAM1 INTEGER ) RETURNS ( RESULT INTEGER )
AS BEGIN ... END

PostgreSQL

CREATE OR REPLACE FUNCTION proc_name( param1 integer ) RETURNS TABLE (
result integer ) LANGUAGE plpgsql AS $$
BEGIN
   ...
END;
$$;

Rules: - Convert procedure to function - Convert RETURNS block to
RETURNS TABLE - Wrap body in \$\$ - Add LANGUAGE plpgsql

------------------------------------------------------------------------

# Parsing Strategy

Avoid naive regex replacement.

Instead:

1.  Tokenize the procedure
2.  Track BEGIN / END nesting depth
3.  Detect IF blocks
4.  Detect loops
5.  Detect SELECT INTO
6.  Transform to a syntax model

------------------------------------------------------------------------

# Internal Syntax Model

Example representation

ProcedureDefinition Name Parameters ReturnFields Variables Statements

Statements may include:

IfStatement ForSelectLoop SelectIntoStatement AssignmentStatement

------------------------------------------------------------------------

# C# Application Structure

Recommended project structure

/Converter Program.cs FileReader.cs InterbaseParser.cs SyntaxModel.cs
PostgresGenerator.cs SyntaxRules.cs

Responsibilities

Program.cs - Accept input and output file paths - Execute pipeline

FileReader.cs - Load SQL text - Normalize whitespace

InterbaseParser.cs - Parse InterBase syntax - Build syntax model

PostgresGenerator.cs - Convert syntax model into PostgreSQL code

SyntaxRules.cs - Encapsulate transformation rules

------------------------------------------------------------------------

# File Processing Pipeline

Read file ↓ Normalize formatting ↓ Parse InterBase syntax ↓ Build syntax
model ↓ Transform to PostgreSQL model ↓ Generate PostgreSQL SQL ↓ Write
output file

------------------------------------------------------------------------

# Edge Cases

The converter must support:

Nested IF blocks

IF (A) THEN BEGIN IF (B) THEN statement; END

Loops inside IF blocks

Multiple INTO variables

Empty BEGIN END blocks

Multiple return columns

------------------------------------------------------------------------

# Testing Strategy

Create test procedures that cover:

1.  Simple IF
2.  IF BEGIN END
3.  Nested IF
4.  SELECT INTO
5.  FOR SELECT loops
6.  SUSPEND usage

Ensure generated PostgreSQL functions compile.

------------------------------------------------------------------------

# Example Conversion

Input

IF (:TOTAL \> 0) THEN BEGIN SELECT COUNT(\*) FROM ORDERS INTO :CNT; END

Output

IF TOTAL \> 0 THEN SELECT COUNT(\*) INTO CNT FROM ORDERS; END IF;

------------------------------------------------------------------------

# Coding Guidelines

-   Use C#
-   Prefer structured parsing over regex
-   Write modular transformation functions
-   Preserve indentation where possible

------------------------------------------------------------------------

# Future Enhancements

Possible improvements

-   ANTLR grammar for InterBase SQL
-   Full SQL tokenizer
-   Automatic PostgreSQL validation
-   Dependency tracking between procedures

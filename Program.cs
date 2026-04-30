using IB2PG;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: IB2PG <input.sql> [--output <output.sql>] [--verbose]");
    return 1;
}

string inputPath  = args[0];
string? outputPath = null;
bool verbose      = false;

for (int i = 1; i < args.Length; i++)
{
    if ((args[i] == "--output" || args[i] == "-o") && i + 1 < args.Length)
        outputPath = args[++i];
    else if (args[i] == "--verbose" || args[i] == "-v")
        verbose = true;
}

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: file not found: {inputPath}");
    return 1;
}

outputPath ??= Path.ChangeExtension(inputPath, ".pg.sql");

try
{
    string sql = FileReader.ReadAndNormalize(inputPath);

    var tokenizer = new Tokenizer(sql);
    var tokens    = tokenizer.Tokenize();

    if (verbose)
    {
        Console.Error.WriteLine($"[verbose] {tokens.Count} tokens");
        foreach (var t in tokens)
            Console.Error.WriteLine($"  {t.Line,4}:{t.Column,-4} {t.Type,-22} {t.RawText}");
    }

    var parser = new InterbaseParser(tokens);
    List<ProcedureDefinition> procs;

    try
    {
        procs = parser.ParseAll();
    }
    catch (ParseException ex)
    {
        Console.Error.WriteLine($"Parse error: {ex.Message}");
        return 1;
    }

    if (verbose)
        Console.Error.WriteLine($"[verbose] {procs.Count} procedure(s) parsed");

    var generator = new PostgresGenerator();
    string output = generator.Generate(procs);

    File.WriteAllText(outputPath, output, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Converted {procs.Count} procedure(s) → {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}

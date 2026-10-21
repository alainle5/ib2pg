using IB2PG;

internal class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  IB2PG <input.sql> [--output <output.pg.sql>] [--verbose]");
            Console.Error.WriteLine("  IB2PG --folder <inputFolder> [--output <outputFolder>] [--verbose]");
            return 1;
        }

        bool verbose = args.Any(a => a is "--verbose" or "-v");

        // Folder batch mode: --folder <dir>
        int folderIdx = Array.IndexOf(args, "--folder");
        if (folderIdx >= 0 && folderIdx + 1 < args.Length)
        {
            string inputFolder  = args[folderIdx + 1];
            string outputFolder = GetNamedArg(args, "--output") ?? GetNamedArg(args, "-o") ?? inputFolder;
            return ConvertFolder(inputFolder, outputFolder, verbose);
        }

        // Single-file mode: first non-flag argument is the input file
        string inputFile   = args[0];
        string? outputFile = GetNamedArg(args, "--output") ?? GetNamedArg(args, "-o");
        outputFile ??= Path.ChangeExtension(inputFile, ".pg.sql");
        return ConvertFile(inputFile, outputFile, verbose);
    }

    private static int ConvertFolder(string inputFolder, string outputFolder, bool verbose)
    {
        if (!Directory.Exists(inputFolder))
        {
            Console.Error.WriteLine($"Error: input folder not found: {inputFolder}");
            return 1;
        }

        Directory.CreateDirectory(outputFolder);

        var files = Directory.EnumerateFiles(inputFolder, "*.sql").ToList();
        Console.WriteLine($"Converting {files.Count} file(s) from '{inputFolder}' to '{outputFolder}'...");

        int errors = 0;
        foreach (var file in files)
        {
            string outName = Path.ChangeExtension(Path.GetFileName(file), ".pg.sql");
            string outPath = Path.Combine(outputFolder, outName);
            if (ConvertFile(file, outPath, verbose) != 0)
                errors++;
        }

        Console.WriteLine(errors == 0
            ? $"Done. {files.Count} file(s) converted successfully."
            : $"Done with {errors} error(s).");
        return errors > 0 ? 1 : 0;
    }

    private static int ConvertFile(string inputFile, string outputFile, bool verbose)
    {
        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"Error: file not found: {inputFile}");
            return 1;
        }

        try
        {
            string sql = FileReader.ReadAndNormalize(inputFile);

            var tokenizer = new Tokenizer(sql);
            var tokens    = tokenizer.Tokenize();

            if (verbose)
            {
                Console.Error.WriteLine($"[verbose] {tokens.Count} tokens in {inputFile}");
                foreach (var t in tokens)
                    Console.Error.WriteLine($"  {t.Line,4}:{t.Column,-4} {t.Type,-22} {t.RawText}");
            }

            List<ProcedureDefinition> procs;
            try
            {
                procs = new InterbaseParser(tokens).ParseAll();
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine($"Parse error in {inputFile}: {ex.Message}");
                return 1;
            }

            if (verbose)
                Console.Error.WriteLine($"[verbose] {procs.Count} procedure(s) parsed");

            string output = new PostgresGenerator().Generate(procs);
            File.WriteAllText(outputFile, output, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"[OK] {procs.Count} procedure(s)  {inputFile}  ->  {outputFile}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error processing {inputFile}: {ex.Message}");
            return 1;
        }
    }

    private static string? GetNamedArg(string[] args, string flag)
    {
        int idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }
}

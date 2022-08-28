using System.CommandLine;
using Fossil_Fighters_Tool.Archive;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Fossil_Fighters_Tool.Command;

public class DecompressCommand : System.CommandLine.Command
{
    public DecompressCommand() : base("decompress", Localization.DecompressCommandDescription)
    {
        var inputArgument = new Argument<string[]>("input", "List of folders or files to extract.") { Arity = ArgumentArity.OneOrMore };
        var outputOption = new Option<string>(new[] { "--output", "-o" }, () => string.Empty, "Output folder to place the extracted contents.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false };
        if (outputOption == null) throw new ArgumentNullException(nameof(outputOption));
        var ExcludeOption = new Option<string[]>(new[] { "--exclude", "-e" }, () => new[] { "**/bin/**/*" }, "Exclude files to be decompressed. You can use wildcard (*) to specify one or more folders.") { Arity = ArgumentArity.OneOrMore, IsRequired = false };

        AddArgument(inputArgument);
        AddOption(outputOption);
        AddOption(ExcludeOption);

        this.SetHandler(Invoke, inputArgument, outputOption, ExcludeOption);
    }

    private void Invoke(string[] inputs, string output, string[] excludes)
    {
        foreach (var input in inputs)
        {
            if (File.Exists(input))
            {
                Decompress(input, output);
            }
            else if (Directory.Exists(input))
            {
                // Input is a directory.
                var matcher = new Matcher();
                matcher.AddInclude("**/*");
                matcher.AddExcludePatterns(excludes);

                foreach (var file in matcher.GetResultsInFullPath(input))
                {
                    Decompress(file, output);
                }
            }
            else
            {
                Console.WriteLine(Localization.InputDoesNotExists, input);
            }
        }
    }

    private void Decompress(string input, string output)
    {
        Console.WriteLine(Localization.FileExtracting, input);

        if (output == string.Empty)
        {
            var directoryName = Path.GetDirectoryName(input)!;
            var fileName = Path.GetFileName(input);

            output = Path.Combine(directoryName, "bin", fileName);
        }
        
        if (!Directory.Exists(output))
        {
            Directory.CreateDirectory(output);
        }

        try
        {
            using var marArchive = new MarArchive(new FileStream(input, FileMode.Open, FileAccess.Read));
            var marEntries = marArchive.Entries;

            for (var i = 0; i < marEntries.Count; i++)
            {
                var outputFile = Path.Combine(output, $"{i}.bin");

                using var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
                using var mcmFileStream = marEntries[i].OpenRead();
                mcmFileStream.CopyTo(outputStream);
                outputStream.Flush();
            
                Console.WriteLine(Localization.FileExtracted, outputFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
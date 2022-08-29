using System.CommandLine;
using Fossil_Fighters_Tool.Archive;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Fossil_Fighters_Tool.Command;

public class CompressCommand : System.CommandLine.Command
{
    public CompressCommand() : base("compress", Localization.CompressCommandDescription)
    {
        var inputArgument = new Argument<string>("input", "Target folder to compress.") { Arity = ArgumentArity.ExactlyOne };
        inputArgument.LegalFilePathsOnly();
        
        var outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file after compression.") { Arity = ArgumentArity.ExactlyOne, IsRequired = true };

        var includeOption = new Option<string[]>(new[] { "--include", "-i" }, () => new[] { "*.bin" }, "Include files to be compressed. You can use wildcard (*) to specify one or more files.") { Arity = ArgumentArity.OneOrMore, IsRequired = false };

        var compressionTypeOption = new Option<McmFileCompressionType[]>(new[] { "--compress-type", "-c" }, () => new[] { McmFileCompressionType.None }, "Type of compression to be used. (Maximum 2) E.g \"-c Huffman -c Lzss\" Compression is done in reverse order. Make sure to put huffman first for better compression ratio.") { Arity = new ArgumentArity(1, 2), IsRequired = false };
        compressionTypeOption.AddCompletions(Enum.GetNames<McmFileCompressionType>());
        
        var maxSizePerChunkOption = new Option<int>(new[] { "--max-size-per-chunk", "-m" }, () => 0x2000, "Split each file into chunks of <size> bytes when compressing.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false, ArgumentHelpName = "size" };

        AddArgument(inputArgument);
        AddOption(outputOption);
        AddOption(includeOption);
        AddOption(compressionTypeOption);
        AddOption(maxSizePerChunkOption);
        
        this.SetHandler(Invoke, inputArgument, outputOption, includeOption, compressionTypeOption, maxSizePerChunkOption);
    }

    private void Invoke(string input, string output, string[] includes, McmFileCompressionType[] compressionTypes, int maxSizePerChunk)
    {
        if (Directory.Exists(input))
        {
            Compress(input, output, includes, compressionTypes, maxSizePerChunk);
        }
        else
        {
            Console.WriteLine(Localization.InputDoesNotExists, input);
        }
    }

    private void Compress(string folder, string output, string[] includes, McmFileCompressionType[] compressionTypes, int maxSizePerChunk)
    {
        using var outputFile = new FileStream(output, FileMode.Create, FileAccess.Write);
        using var marArchive = new MarArchive(outputFile, MarArchiveMode.Create);
        
        var matcher = new Matcher();
        matcher.AddIncludePatterns(includes);

        foreach (var file in matcher.GetResultsInFullPath(folder).OrderBy(s => s))
        {
            var marArchiveEntry = marArchive.CreateEntry();
            
            using var mcmFileStream = marArchiveEntry.OpenWrite();
            mcmFileStream.MaxSizePerChunk = maxSizePerChunk;

            if (compressionTypes.Length == 1)
            {
                mcmFileStream.CompressionType1 = compressionTypes[0];
            }
            else
            {
                mcmFileStream.CompressionType1 = compressionTypes[0];
                mcmFileStream.CompressionType2 = compressionTypes[1];
            }
            
            using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            fileStream.CopyTo(mcmFileStream);
            mcmFileStream.Flush();
        }
        
        marArchive.Flush();
    }
}
// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2023 Yong Jian Ming
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing;
using TheDialgaTeam.FossilFighters.Assets;
using TheDialgaTeam.FossilFighters.Assets.Archive;

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Command;

internal sealed class CompressCommand : System.CommandLine.Command
{
    public CompressCommand() : base("compress", Localization.CompressCommandDescription)
    {
        var inputArgument = new Argument<string>("input", "Target folder to compress.") { Arity = ArgumentArity.ExactlyOne };
        inputArgument.LegalFilePathsOnly();

        var outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file after compression.") { Arity = ArgumentArity.ExactlyOne, IsRequired = true, ArgumentHelpName = "file" };

        var includeOption = new Option<string[]>(new[] { "--include", "-i" }, static () => new[] { "*.bin" }, "Include files to be compressed. You can use wildcard (*) to specify one or more files. E.g \"-i *.bin -i *.hex\"") { Arity = ArgumentArity.OneOrMore, IsRequired = false, ArgumentHelpName = "fileTypes" };

        var compressionTypeOption = new Option<McmFileCompressionType[]>(new[] { "--compress-type", "-c" }, static () => Array.Empty<McmFileCompressionType>(), "Type of compression to be used. (Maximum 2) E.g \"-c Huffman -c Lzss\" Compression is done in reverse order. Make sure to put huffman first for better compression ratio.") { Arity = new ArgumentArity(1, 2), IsRequired = false };
        compressionTypeOption.AddCompletions(Enum.GetNames<McmFileCompressionType>());

        var maxSizePerChunkOption = new Option<uint>(new[] { "--max-size-per-chunk", "-m" }, static () => 0x2000, "Split each file into chunks of <size> bytes when compressing.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false, ArgumentHelpName = "size" };

        var metaFileOption = new Option<string>(new[] { "--meta-file", "-mf" }, static () => "meta.json", "Meta definition file to define the compression type and the chunk size.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false, ArgumentHelpName = "file" };

        AddArgument(inputArgument);
        AddOption(outputOption);
        AddOption(includeOption);
        AddOption(compressionTypeOption);
        AddOption(maxSizePerChunkOption);
        AddOption(metaFileOption);

        this.SetHandler(Invoke, inputArgument, outputOption, includeOption, compressionTypeOption, maxSizePerChunkOption, metaFileOption);
    }

    private static void Invoke(string input, string output, string[] includes, McmFileCompressionType[] compressionTypes, uint maxSizePerChunk, string metaFile)
    {
        if (Directory.Exists(input))
        {
            Compress(input, output, includes, compressionTypes, maxSizePerChunk, metaFile);
        }
        else
        {
            Console.WriteLine(Localization.InputDoesNotExists, input);
        }
    }

    private static void Compress(string inputFolder, string outputFile, IEnumerable<string> includes, IReadOnlyList<McmFileCompressionType> compressionTypes, uint maxSizePerChunk, string metaFile)
    {
        using var outputFileStream = File.OpenWrite(outputFile);
        using var marArchive = new MarArchive(outputFileStream, MarArchiveMode.Create);

        var matcher = new Matcher();
        matcher.AddIncludePatterns(includes);

        if (Path.GetDirectoryName(outputFile) == inputFolder)
        {
            matcher.AddExclude(Path.GetFileName(outputFile));
        }

        Dictionary<int, McmFileMetadata>? mcmMetadata = null;

        if (compressionTypes.Count == 0)
        {
            if (File.Exists(metaFile))
            {
                mcmMetadata = JsonSerializer.Deserialize(File.OpenRead(metaFile), CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata);
            }
            else
            {
                var mcmMetaFilePath = Path.GetFullPath(Path.Combine(inputFolder, metaFile));

                if (File.Exists(mcmMetaFilePath))
                {
                    mcmMetadata = JsonSerializer.Deserialize(File.OpenRead(mcmMetaFilePath), CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata);
                }
            }
        }

        foreach (var file in matcher.GetResultsInFullPath(inputFolder)
                     .Where(s => int.TryParse(Path.GetFileNameWithoutExtension(s), out var _))
                     .OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s)))
                     .ToArray())
        {
            var marArchiveEntry = marArchive.CreateEntry();
            using var mcmFileStream = marArchiveEntry.OpenWrite();

            if (mcmMetadata is not null)
            {
                mcmFileStream.LoadMetadata(mcmMetadata[int.Parse(Path.GetFileNameWithoutExtension(file))]);
            }
            else
            {
                mcmFileStream.MaxSizePerChunk = maxSizePerChunk;

                if (compressionTypes.Count == 1)
                {
                    mcmFileStream.CompressionType1 = compressionTypes[0];
                }
                else
                {
                    if (compressionTypes[0] == McmFileCompressionType.None && compressionTypes[1] != McmFileCompressionType.None)
                    {
                        mcmFileStream.CompressionType1 = compressionTypes[1];
                    }
                    else
                    {
                        mcmFileStream.CompressionType1 = compressionTypes[0];
                        mcmFileStream.CompressionType2 = compressionTypes[1];
                    }
                }
            }

            Console.WriteLine(Localization.CompressCommand_Compress_Compressing_File, file);

            using var fileStream = File.OpenRead(file);
            fileStream.CopyTo(mcmFileStream);
        }
    }
}
﻿// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2022 Yong Jian Ming
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
using Microsoft.Extensions.FileSystemGlobbing;
using TheDialgaTeam.FossilFighters.Assets.Archive;

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Command;

internal sealed class CompressCommand : System.CommandLine.Command
{
    public CompressCommand() : base("compress", Localization.CompressCommandDescription)
    {
        var inputArgument = new Argument<string>("input", "Target folder to compress.") { Arity = ArgumentArity.ExactlyOne };
        inputArgument.LegalFilePathsOnly();

        var outputOption = new Option<string>(new[] { "--output", "-o" }, "Output file after compression.") { Arity = ArgumentArity.ExactlyOne, IsRequired = true };

        var includeOption = new Option<string[]>(new[] { "--include", "-i" }, () => new[] { "*.bin" }, "Include files to be compressed. You can use wildcard (*) to specify one or more files.") { Arity = ArgumentArity.OneOrMore, IsRequired = false };

        var compressionTypeOption = new Option<McmFileCompressionType[]>(new[] { "--compress-type", "-c" }, () => new[] { McmFileCompressionType.None }, "Type of compression to be used. (Maximum 2) E.g \"-c Huffman -c Lzss\" Compression is done in reverse order. Make sure to put huffman first for better compression ratio.") { Arity = new ArgumentArity(1, 2), IsRequired = false };
        compressionTypeOption.AddCompletions(Enum.GetNames<McmFileCompressionType>());

        var maxSizePerChunkOption = new Option<uint>(new[] { "--max-size-per-chunk", "-m" }, () => 0x2000, "Split each file into chunks of <size> bytes when compressing.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false, ArgumentHelpName = "size" };

        AddArgument(inputArgument);
        AddOption(outputOption);
        AddOption(includeOption);
        AddOption(compressionTypeOption);
        AddOption(maxSizePerChunkOption);

        this.SetHandler(Invoke, inputArgument, outputOption, includeOption, compressionTypeOption, maxSizePerChunkOption);
    }

    private void Invoke(string input, string output, string[] includes, McmFileCompressionType[] compressionTypes, uint maxSizePerChunk)
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

    private void Compress(string folder, string output, string[] includes, McmFileCompressionType[] compressionTypes, uint maxSizePerChunk)
    {
        using var outputFile = new FileStream(output, FileMode.Create, FileAccess.Write);
        using var marArchive = new MarArchive(outputFile, MarArchiveMode.Create);

        var matcher = new Matcher();
        matcher.AddIncludePatterns(includes);

        foreach (var file in matcher.GetResultsInFullPath(folder).OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s))))
        {
            var marArchiveEntry = marArchive.CreateEntry();
            using var mcmFileStream = marArchiveEntry.OpenWrite();

            var mcmMetaFilePath = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(file)}.meta");
            
            if (File.Exists(mcmMetaFilePath))
            {
                mcmFileStream.LoadFileMetadata(mcmMetaFilePath);
            }
            else
            {
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
            }
            
            using var fileStream = File.OpenRead(file);
            fileStream.CopyTo(mcmFileStream);
        }
    }
}
// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
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
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using SixLabors.ImageSharp;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.Header;
using TheDialgaTeam.FossilFighters.Assets.Image;
using TheDialgaTeam.FossilFighters.Assets.Motion;
using ColorPalette = TheDialgaTeam.FossilFighters.Assets.Motion.ColorPalette;

namespace TheDialgaTeam.FossilFighters.Tool.Cli.Command;

public sealed class DecompressCommand : System.CommandLine.Command
{
    public DecompressCommand() : base("decompress", Localization.DecompressCommandDescription)
    {
        var inputArgument = new Argument<string[]>("input", "List of folders or files to extract.") { Arity = ArgumentArity.OneOrMore };
        var outputOption = new Option<string>(new[] { "--output", "-o" }, () => string.Empty, "Output folder to place the extracted contents.") { Arity = ArgumentArity.ExactlyOne, IsRequired = false };
        var excludeOption = new Option<string[]>(new[] { "--exclude", "-e" }, () => new[] { "**/bin/**/*" }, "Exclude files to be decompressed. You can use wildcard (*) to specify one or more folders.") { Arity = ArgumentArity.OneOrMore, IsRequired = false };

        AddArgument(inputArgument);
        AddOption(outputOption);
        AddOption(excludeOption);

        this.SetHandler(Invoke, inputArgument, outputOption, excludeOption);
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
        else
        {
            var fileName = Path.GetFileName(input);
            output = Path.Combine(output, fileName);
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

                using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                {
                    using (var mcmFileStream = marEntries[i].OpenRead())
                    {
                        mcmFileStream.CopyTo(outputStream);
                    }
                }

                Console.WriteLine(Localization.FileExtracted, outputFile);

                // Handle Add-Ons (Image)
                using (var fileStream = new FileStream(outputFile, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = new BinaryReader(fileStream, Encoding.UTF8))
                    {
                        switch (reader.ReadInt32())
                        {
                            case MmsHeader.FileHeader:
                            {
                                fileStream.Seek(0, SeekOrigin.Begin);
                                var mmsHeader = MmsHeader.GetHeaderFromStream(fileStream);
                                var colorPalettes = new ColorPalette[mmsHeader.ColorPaletteFileCount];

                                for (var j = 0; j < mmsHeader.ColorPaletteFileCount; j++)
                                {
                                    var colorPaletteFile = Path.Combine(output, "..", mmsHeader.ColorPaletteFileName, $"{mmsHeader.ColorPaletteFileIndexes[j]}.bin");

                                    if (!File.Exists(colorPaletteFile))
                                    {
                                        Decompress(Path.Combine(Path.GetDirectoryName(input)!, mmsHeader.ColorPaletteFileName), Path.Combine(output, ".."));
                                    }

                                    using var colorPaletteFileStream = new FileStream(colorPaletteFile, FileMode.Open, FileAccess.Read);
                                    colorPalettes[j] = MotionUtility.GetColorPalette(colorPaletteFileStream);
                                }

                                for (var j = 0; j < mmsHeader.BitmapFileCount; j++)
                                {
                                    var bitmapFile = Path.Combine(output, "..", mmsHeader.BitmapFileName, $"{mmsHeader.BitmapFileIndexes[j]}.bin");

                                    if (!File.Exists(bitmapFile))
                                    {
                                        Decompress(Path.Combine(Path.GetDirectoryName(input)!, mmsHeader.BitmapFileName), Path.Combine(output, ".."));
                                    }

                                    using var bitmapFileStream = new FileStream(bitmapFile, FileMode.Open, FileAccess.Read);
                                    var bitmap = MotionUtility.GetBitmap(bitmapFileStream);

                                    for (var k = 0; k < colorPalettes.Length; k++)
                                    {
                                        using var image = MotionUtility.GetImage(colorPalettes[k], bitmap);
                                        var imageOutputFilePath = Path.Combine(output, $"{mmsHeader.BitmapFileIndexes[j]}_{k}.png");
                                        image.SaveAsPng(imageOutputFilePath);

                                        Console.WriteLine(Localization.FileExtracted, imageOutputFilePath);
                                    }
                                }

                                break;
                            }

                            case MpmHeader.FileHeader:
                            {
                                fileStream.Seek(0, SeekOrigin.Begin);
                                var mpmHeader = MpmHeader.GetHeaderFromStream(fileStream);

                                var colorPaletteFile = Path.Combine(output, "..", mpmHeader.ColorPaletteFileName, $"{mpmHeader.ColorPaletteFileIndex}.bin");

                                if (!File.Exists(colorPaletteFile))
                                {
                                    Decompress(Path.Combine(Path.GetDirectoryName(input)!, mpmHeader.ColorPaletteFileName), Path.Combine(output, ".."));
                                }

                                using var colorPaletteFileStream = new FileStream(colorPaletteFile, FileMode.Open, FileAccess.Read);
                                var colorPalette = ImageUtility.GetColorPalette(colorPaletteFileStream);

                                var bitmapFile = Path.Combine(output, "..", mpmHeader.BitmapFileName, $"{mpmHeader.BitmapFileIndex}.bin");

                                if (!File.Exists(bitmapFile))
                                {
                                    Decompress(Path.Combine(Path.GetDirectoryName(input)!, mpmHeader.BitmapFileName), Path.Combine(output, ".."));
                                }

                                if (mpmHeader.BgMapFileIndex == 0)
                                {
                                    using var bitmapFileStream = new FileStream(bitmapFile, FileMode.Open, FileAccess.Read);
                                    var bitmap = ImageUtility.GetBitmap(bitmapFileStream);

                                    using var image = ImageUtility.GetImage(mpmHeader, colorPalette, bitmap);
                                    var imageOutputFilePath = Path.Combine(output, $"{mpmHeader.BitmapFileIndex}.png");
                                    image.SaveAsPng(imageOutputFilePath);

                                    Console.WriteLine(Localization.FileExtracted, imageOutputFilePath);
                                }
                                else
                                {
                                    var bitmapIndexFile = Path.Combine(output, "..", mpmHeader.BgMapFileName, $"{mpmHeader.BgMapFileIndex}.bin");

                                    if (!File.Exists(bitmapFile))
                                    {
                                        Decompress(Path.Combine(Path.GetDirectoryName(input)!, mpmHeader.BgMapFileName), Path.Combine(output, ".."));
                                    }

                                    using var bitmapFileStream = new FileStream(bitmapFile, FileMode.Open, FileAccess.Read);
                                    using var bitmapIndexFileStream = new FileStream(bitmapIndexFile, FileMode.Open, FileAccess.Read);

                                    var chunkBitmap = ImageUtility.GetChunkBitmap(colorPalette, bitmapFileStream, bitmapIndexFileStream);

                                    using var image = ImageUtility.GetImage(mpmHeader, colorPalette, chunkBitmap);
                                    var imageOutputFilePath = Path.Combine(output, $"{mpmHeader.BitmapFileIndex}.png");
                                    image.SaveAsPng(imageOutputFilePath);

                                    Console.WriteLine(Localization.FileExtracted, imageOutputFilePath);
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }
}
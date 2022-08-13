using System.Text;
using Fossil_Fighters_Tool.Archive;
using Fossil_Fighters_Tool.Header;
using Fossil_Fighters_Tool.Motion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool;

internal static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length < 1) return;

        var inputFilePath = args[0];

        if (Directory.Exists(inputFilePath))
        {
            foreach (var file in Directory.EnumerateFiles(inputFilePath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (Array.Exists(Path.GetDirectoryName(file)!.Split(Path.DirectorySeparatorChar), s => s.Equals("bin"))) continue;
                    ExtractMarArchive(file);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
            }
            
            return;
        }

        if (!File.Exists(inputFilePath))
        {
            Console.WriteLine("File does not exist.");
            return;
        }

        try
        {
            ExtractMarArchive(inputFilePath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
        }
    }
    
    private static void ExtractMarArchive(string inputFilePath)
    {
        Console.WriteLine($"Extracting: {inputFilePath}");
        
        var directoryName = Path.GetDirectoryName(inputFilePath)!;
        var fileName = Path.GetFileName(inputFilePath);
        
        using var marArchive = new MarArchive(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read));
        var marEntries = marArchive.Entries;
        
        for (var i = 0; i < marEntries.Count; i++)
        {
            var outputDirectory = Path.Combine(directoryName, "bin", fileName);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
            
            var outputFile = Path.Combine(outputDirectory, $"{i}.bin");

            using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            {
                using var mcmFileStream = new McmFileStream(outputStream, McmFileStreamMode.Decompress);
                using var inputStream = marEntries[i].Open();
                inputStream.CopyTo(mcmFileStream);
            }

            using var binaryReader = new BinaryReader(new FileStream(outputFile, FileMode.Open, FileAccess.Read), Encoding.ASCII);

            switch (binaryReader.ReadInt32())
            {
                case MmsFileReader.Id:
                {
                    using var mmsFileReader = new MmsFileReader(binaryReader);

                    var colorPaletteFileReaders = new ColorPaletteFileReader[mmsFileReader.ColorPaletteFileCount];

                    for (var j = 0; j < mmsFileReader.ColorPaletteFileCount; j++)
                    {
                        var colorPaletteFile = Path.Combine(outputDirectory, "..", mmsFileReader.ColorPaletteFileName, $"{mmsFileReader.ColorPaletteFileIndexes[j]}.bin");

                        if (!File.Exists(colorPaletteFile))
                        {
                            ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mmsFileReader.ColorPaletteFileName));
                        }
                        
                        using var colorPaletteFileReader = new ColorPaletteFileReader(new BinaryReader(new FileStream(colorPaletteFile, FileMode.Open, FileAccess.Read)));
                        colorPaletteFileReaders[j] = colorPaletteFileReader;
                    }

                    for (var j = 0; j < mmsFileReader.BitmapFileCount; j++)
                    {
                        var bitmapFile = Path.Combine(outputDirectory, "..", mmsFileReader.BitmapFileName, $"{mmsFileReader.BitmapFileIndexes[j]}.bin");

                        if (!File.Exists(bitmapFile))
                        {
                            ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mmsFileReader.BitmapFileName));
                        }

                        using var bitmapFileReader = new BitmapFileReader(new BinaryReader(new FileStream(bitmapFile, FileMode.Open, FileAccess.Read)));

                        var width = bitmapFileReader.Width == 0 ? 16 : bitmapFileReader.Width;
                        var height = bitmapFileReader.Height == 0 ? 16 : bitmapFileReader.Height;

                        using var image = new Image<Rgba32>(width, height);

                        var bitmapIndex = 0;
                        var gridX = 0;
                        var gridY = 0;

                        if (bitmapFileReader.ColorType == 0)
                        {
                            while (bitmapIndex * 2 < width * height)
                            {
                                for (var y = 0; y < 8; y++)
                                {
                                    for (var x = 0; x < 8; x += 2)
                                    {
                                        // TODO: Which color palette to use? God knows...

                                        int colorPaletteIndex;

                                        if (mmsFileReader.BitmapFileCount < mmsFileReader.ColorPaletteFileCount)
                                        {
                                            colorPaletteIndex = 0;
                                        }
                                        else
                                        {
                                            colorPaletteIndex = j / (int) Math.Ceiling((double) mmsFileReader.BitmapFileCount / (double) mmsFileReader.ColorPaletteFileCount);
                                        }
                                        
                                        image[x + gridX * 8, y + gridY * 8] = colorPaletteFileReaders[colorPaletteIndex].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex] >> 4];
                                        image[x + 1 + gridX * 8, y + gridY * 8] = colorPaletteFileReaders[colorPaletteIndex].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex] & 0xF];

                                        
                                        bitmapIndex++;
                                    }
                                }

                                gridX++;

                                if (gridX >= width / 8)
                                {
                                    gridX = 0;
                                    gridY++;
                                }
                            }
                        }
                        else if (bitmapFileReader.ColorType == 1)
                        {
                            while (bitmapIndex < width * height)
                            {
                                for (var y = 0; y < 8; y++)
                                {
                                    for (var x = 0; x < 8; x++)
                                    {
                                        image[x + gridX * 8, y + gridY * 8] = colorPaletteFileReaders[0].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex++]];
                                    }
                                }

                                gridX++;

                                if (gridX >= width / 8)
                                {
                                    gridX = 0;
                                    gridY++;
                                }
                            }
                        }

                        image.SaveAsPng(Path.Combine(outputDirectory, $"{mmsFileReader.BitmapFileIndexes[j]}.png"));
                        Console.WriteLine($"Extracted: {Path.Combine(outputDirectory, $"{mmsFileReader.BitmapFileIndexes[j]}.png")}");
                    }
                    
                    break;
                }
            }
        }
    }
    
    private static void TestFile(string inputFilePath)
    {
        var inputFileDirectory = Path.GetDirectoryName(inputFilePath)!;

        using var testColor = new Fossil_Fighters_Tool.Image.ColorPaletteFileReader(File.OpenRead(Path.Combine(inputFileDirectory, "0.bin")));
        var test = File.ReadAllBytes(inputFilePath);

        var width = 256;
        var height = 32;
        
        using var image = new Image<Rgba32>(width, height);

        var bitmapIndex = 0;
        var gridX = 0;
        var gridY = 0;
        
        while (bitmapIndex < width * height)
        {
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x += 1)
                {
                    // TODO: Which color palette to use? God knows...
                    //image[x + gridX * 8, y + gridY * 8] = testColor.ColorTable[test[bitmapIndex] & 0xF];
                    //image[x + 1 + gridX * 8, y + gridY * 8] = testColor.ColorTable[test[bitmapIndex] >> 4];
                    image[x + gridX * 8, y + gridY * 8] = testColor.ColorTable[test[bitmapIndex]];
                        
                    bitmapIndex++;
                }
            }

            gridX++;

            if (gridX >= width / 8)
            {
                gridX = 0;
                gridY++;
            }
        }
        
        image.SaveAsPng(Path.Combine(inputFileDirectory, "0.png"));
    }
}
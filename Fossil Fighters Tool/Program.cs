using System.Text;
using Fossil_Fighters_Tool.Archive;
using Fossil_Fighters_Tool.Archive.Compression.Huffman;
using Fossil_Fighters_Tool.Header;
using Fossil_Fighters_Tool.Image;
using Fossil_Fighters_Tool.Motion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool;

internal static class Program
{
    public static void Main(string[] args)
    {
        var huffman = new HuffmanStream(File.Open("C:\\Users\\jianmingyong\\Desktop\\Rom Hacking Tools\\2.bin", FileMode.Create), HuffmanStreamMode.Compress);
        File.OpenRead("C:\\Users\\jianmingyong\\Desktop\\Rom Hacking Tools\\0.bin").CopyTo(huffman);
        huffman.Flush();
        huffman.Dispose();

        var test = new HuffmanStream(File.OpenRead("C:\\Users\\jianmingyong\\Desktop\\Rom Hacking Tools\\2.bin"), HuffmanStreamMode.Decompress);
        var test2 = File.Open("C:\\Users\\jianmingyong\\Desktop\\Rom Hacking Tools\\4.bin", FileMode.Create);
        test.CopyTo(test2);
        test2.Flush();
        
        //var huffman = new HuffmanStream(File.OpenRead("C:\\Users\\jianmingyong\\Desktop\\Rom Hacking Tools\\3.bin"), HuffmanStreamMode.Decompress);
        //huffman.CopyTo(new MemoryStream());
        
        return;
        
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
        Console.WriteLine(Localization.FileExtracting, inputFilePath);
        
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

            using var fileStream = new FileStream(outputFile, FileMode.Open, FileAccess.Read);
            using var binaryReader = new BinaryReader(fileStream, Encoding.UTF8);

            switch (binaryReader.ReadInt32())
            {
                case MmsFileReader.Id:
                {
                    using var mmsFileReader = new MmsFileReader(binaryReader);

                    var colorPalettes = new ColorPalette[mmsFileReader.ColorPaletteFileCount];

                    for (var j = 0; j < mmsFileReader.ColorPaletteFileCount; j++)
                    {
                        var colorPaletteFile = Path.Combine(outputDirectory, "..", mmsFileReader.ColorPaletteFileName, $"{mmsFileReader.ColorPaletteFileIndexes[j]}.bin");

                        if (!File.Exists(colorPaletteFile))
                        {
                            ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mmsFileReader.ColorPaletteFileName));
                        }

                        using var colorPaletteFileStream = new FileStream(colorPaletteFile, FileMode.Open, FileAccess.Read);
                        colorPalettes[j] = MotionUtility.GetColorPalette(colorPaletteFileStream);
                    }

                    for (var j = 0; j < mmsFileReader.BitmapFileCount; j++)
                    {
                        var bitmapFile = Path.Combine(outputDirectory, "..", mmsFileReader.BitmapFileName, $"{mmsFileReader.BitmapFileIndexes[j]}.bin");

                        if (!File.Exists(bitmapFile))
                        {
                            ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mmsFileReader.BitmapFileName));
                        }

                        using var bitmapFileStream = new FileStream(bitmapFile, FileMode.Open, FileAccess.Read);
                        var bitmap = MotionUtility.GetBitmap(bitmapFileStream);
                        
                        var width = bitmap.Width;
                        var height = bitmap.Height;

                        using var image = new Image<Rgba32>(width, height);

                        var bitmapIndex = 0;
                        var gridX = 0;
                        var gridY = 0;

                        if (bitmap.ColorPaletteType == ColorPaletteType.Color16)
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
                                            colorPaletteIndex = Math.Max(j / (int) Math.Ceiling(mmsFileReader.BitmapFileCount / (double) mmsFileReader.ColorPaletteFileCount), mmsFileReader.ColorPaletteFileCount - 1);
                                        }
                                        
                                        image[x + gridX * 8, y + gridY * 8] = colorPalettes[colorPaletteIndex].Table[bitmap.ColorPaletteIndexes[bitmapIndex] >> 4];
                                        image[x + 1 + gridX * 8, y + gridY * 8] = colorPalettes[colorPaletteIndex].Table[bitmap.ColorPaletteIndexes[bitmapIndex] & 0xF];

                                        
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
                        else if (bitmap.ColorPaletteType == ColorPaletteType.Color256)
                        {
                            while (bitmapIndex < width * height)
                            {
                                for (var y = 0; y < 8; y++)
                                {
                                    for (var x = 0; x < 8; x++)
                                    {
                                        image[x + gridX * 8, y + gridY * 8] = colorPalettes[0].Table[bitmap.ColorPaletteIndexes[bitmapIndex++]];
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
                        Console.WriteLine(Localization.FileExtracted, Path.Combine(outputDirectory, $"{mmsFileReader.BitmapFileIndexes[j]}.png"));
                    }
                    
                    break;
                }

                case MpmHeader.Id:
                {
                    var mpmHeader = MpmHeader.GetHeaderFromStream(fileStream);
                    var colorPaletteFilePath = Path.Combine(outputDirectory, "..", mpmHeader.ColorPaletteFileName, $"{mpmHeader.ColorPaletteFileIndex}.bin");
                    var bitmapFilePath = Path.Combine(outputDirectory, "..", mpmHeader.BitmapFileName, $"{mpmHeader.BitmapFileIndex}.bin");

                    if (!File.Exists(colorPaletteFilePath))
                    {
                        ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mpmHeader.ColorPaletteFileName));
                    }
                    
                    if (!File.Exists(bitmapFilePath))
                    {
                        ExtractMarArchive(Path.Combine(outputDirectory, "..", "..", mpmHeader.BitmapFileName));
                    }

                    using var colorPaletteFile = new FileStream(colorPaletteFilePath, FileMode.Open, FileAccess.Read);
                    using var bitmapFile = new FileStream(bitmapFilePath, FileMode.Open, FileAccess.Read);
                    
                    var colorPalette = ImageUtility.GetColorPalette(colorPaletteFile);
                    var bitmap = ImageUtility.GetBitmap(bitmapFile);
                    
                    using var image = new Image<Rgba32>(mpmHeader.Width, mpmHeader.Height);
                    var bitmapIndex = 0;

                    if (mpmHeader.Unknown7 != 0)
                    {
                        var gridX = 0;
                        var gridY = 0;

                        if (colorPalette.Length == 16)
                        {
                            while (bitmapIndex * 2 < mpmHeader.Width * mpmHeader.Height)
                            {
                                for (var y = 0; y < 8; y++)
                                {
                                    for (var x = 0; x < 8; x += 2)
                                    {
                                        image[x + gridX * 8, y + gridY * 8] = colorPalette[bitmap[bitmapIndex] >> 4];
                                        image[x + 1 + gridX * 8, y + gridY * 8] = colorPalette[bitmap[bitmapIndex] & 0xF];
                                        bitmapIndex++;
                                    }
                                }

                                gridX++;

                                if (gridX >= mpmHeader.Width / 8)
                                {
                                    gridX = 0;
                                    gridY++;
                                }
                            }
                        }
                        else
                        {
                            while (bitmapIndex < mpmHeader.Width * mpmHeader.Height)
                            {
                                for (var y = 0; y < 8; y++)
                                {
                                    for (var x = 0; x < 8; x++)
                                    {
                                        image[x + gridX * 8, y + gridY * 8] = colorPalette[bitmap[bitmapIndex]];
                                        bitmapIndex++;
                                    }
                                }

                                gridX++;

                                if (gridX >= mpmHeader.Width / 8)
                                {
                                    gridX = 0;
                                    gridY++;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (colorPalette.Length == 16)
                        {
                            for (var y = 0; y < mpmHeader.Height; y++)
                            {
                                for (var x = 0; x < mpmHeader.Width; x += 2)
                                {
                                    image[x, y] = colorPalette[bitmap[bitmapIndex] >> 4];
                                    image[x + 1, y] = colorPalette[bitmap[bitmapIndex] & 0xF];
                                    bitmapIndex++;
                                }
                            }
                        }
                        else
                        {
                            for (var y = 0; y < mpmHeader.Height; y++)
                            {
                                for (var x = 0; x < mpmHeader.Width; x++)
                                {
                                    image[x, y] = colorPalette[bitmap[bitmapIndex]];
                                    bitmapIndex++;
                                }
                            }
                        }
                    }

                    image.SaveAsPng(Path.Combine(outputDirectory, $"{mpmHeader.BitmapFileIndex}.png"));
                    Console.WriteLine(Localization.FileExtracted, Path.Combine(outputDirectory, $"{mpmHeader.BitmapFileIndex}.png"));
                    break;
                }
            }
        }
    }
    
    private static void TestFile(string inputFilePath)
    {
        var inputFileDirectory = Path.GetDirectoryName(inputFilePath)!;

        using var colorPaletteFile = File.OpenRead(Path.Combine(inputFileDirectory, "1.bin"));
        var colorPalette = ImageUtility.GetColorPalette(colorPaletteFile);

        var colorIndexesFile = File.OpenRead(Path.Combine(inputFileDirectory, "2.bin"));
        var colorIndexes = ImageUtility.GetBitmap(colorIndexesFile);
        
        var width = 256;
        var height = 256;
        
        using var image = new Image<Rgba32>(width, height);

        var bitmapIndex = 0;

        if (colorPalette.Length == 16)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x += 2)
                {
                    image[x, y] = colorPalette[colorIndexes[bitmapIndex] & 0xF];
                    image[x, y] = colorPalette[colorIndexes[bitmapIndex] >> 4];

                    bitmapIndex++;
                }
            }
        }
        else
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    image[x, y] = colorPalette[colorIndexes[bitmapIndex]];
                    bitmapIndex++;
                }
            }
        }

        image.SaveAsPng(Path.Combine(inputFileDirectory, "0.png"));
    }
}
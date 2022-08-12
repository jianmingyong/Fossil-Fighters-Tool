using Fossil_Fighters_Tool.Archive.Compression.Huffman;
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
                    Console.WriteLine($"Extracting: {file}");
                    ExtractMarFile(file);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            
            return;
        }

        if (!File.Exists(inputFilePath))
        {
            Console.WriteLine("File does not exist.");
            return;
        }

        using var output = new MemoryStream();
        using var test = new HuffmanStream(output, HuffmanStreamMode.Decompress, true);
        using var file2 = File.OpenRead(inputFilePath);
        file2.CopyTo(test);

        try
        {
            ExtractMarFile(inputFilePath);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    private static void ExtractMarFile(string inputFilePath)
    {
        using var marFileReader = new MarFileReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read));

        var directoryName = Path.GetDirectoryName(inputFilePath)!;
        var fileName = Path.GetFileName(inputFilePath);
        var marExtractedDirectory = Path.Combine(directoryName, "bin", fileName);
        var isModelFolder = Array.Exists(marExtractedDirectory.Split(Path.DirectorySeparatorChar), s => s.Equals("model"));

        marFileReader.ExtractTo(marExtractedDirectory);
    
        for (var i = 0; i < marFileReader.FilePointers.Length; i++)
        {
            using var mcmFileReader = new McmFileReaderOld(new FileStream(Path.Combine(marExtractedDirectory, $"{i}.mcm"), FileMode.Open));
            mcmFileReader.ExtractTo(Path.Combine(marExtractedDirectory, i.ToString()));

            for (int j = 0; j < mcmFileReader.FilePointers.Length; j++)
            {
                try
                {
                    if (!isModelFolder)
                    {
                        ExtractHuffmanFile(Path.Combine(marExtractedDirectory, i.ToString(), $"{j}.bin"));
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
                
                try
                {
                    ExtractMmsFile(Path.Combine(marExtractedDirectory, i.ToString(), $"{j}.bin"));
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }

    private static void ExtractHuffmanFile(string inputFilePath)
    {
        using var huffmanFileReader = new HuffmanFileReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read));
        var inputFileDirectory = Path.GetDirectoryName(inputFilePath)!;
        huffmanFileReader.Decompress(inputFileDirectory);
    }
    
    private static void ExtractMmsFile(string inputFilePath)
    {
        using var mmsFileReader = new MmsFileReader(new FileStream(inputFilePath, FileMode.Open, FileAccess.Read));
        
        var inputFileDirectory = Path.GetDirectoryName(inputFilePath)!;
        var colorPalettes = new List<ColorPaletteFileReader>();

        for (var i = 0; i < mmsFileReader.ColorPaletteFileIndexes.Length; i++)
        {
            var colorPaletteFile = Path.Combine(inputFileDirectory, "..", "..", mmsFileReader.ColorPaletteFileName, mmsFileReader.ColorPaletteFileIndexes[i].ToString(), "0.bin");

            if (!File.Exists(colorPaletteFile))
            {
                ExtractMarFile(Path.Combine(inputFileDirectory, "..", "..", "..", mmsFileReader.ColorPaletteFileName));
            }
            
            using var colorPaletteFileReader = new ColorPaletteFileReader(new FileStream(colorPaletteFile, FileMode.Open, FileAccess.Read));
            colorPalettes.Add(colorPaletteFileReader);
        }
        
        for (var i = 0; i < mmsFileReader.BitmapFileIndexes.Length; i++)
        {
            try
            {
                var bitmapFile = Path.Combine(inputFileDirectory, "..", "..", mmsFileReader.BitmapFileName, mmsFileReader.BitmapFileIndexes[i].ToString(), "0.bin");
            
                using var bitmapFileReader = new BitmapFileReader(new FileStream(bitmapFile, FileMode.Open, FileAccess.Read));

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
                                image[x + gridX * 8, y + gridY * 8] = colorPalettes[i / (mmsFileReader.BitmapFileIndexes.Length / colorPalettes.Count)].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex] >> 4];
                                image[x + 1 + gridX * 8, y + gridY * 8] = colorPalettes[i / (mmsFileReader.BitmapFileIndexes.Length / colorPalettes.Count)].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex] & 0xF];
                                
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
                                image[x + gridX * 8, y + gridY * 8] = colorPalettes[0].ColorTable[bitmapFileReader.BitmapColorIndexes[bitmapIndex++]];
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
                
                image.SaveAsPng(Path.Combine(inputFileDirectory, $"{mmsFileReader.BitmapFileIndexes[i]}.png"));
                Console.WriteLine($"Extracted: {Path.Combine(inputFileDirectory, $"{mmsFileReader.BitmapFileIndexes[i]}.png")}");
            }
            catch (Exception)
            {
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
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using JetBrains.Annotations;
using SixLabors.ImageSharp.PixelFormats;
using TheDialgaTeam.FossilFighters.Assets.Header;

namespace TheDialgaTeam.FossilFighters.Assets.Image;

[PublicAPI]
public static class ImageUtility
{
    private const int ColorPalette16FileSize = 16 * 2;
    private const int ColorPalette256FileSize = 256 * 2;
    
    public static ColorPalette GetColorPalette(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ColorPalette256FileSize + 1);

        try
        {
            var bytesRead = stream.Read(buffer, 0, ColorPalette256FileSize + 1);
            if (bytesRead != ColorPalette16FileSize && bytesRead != ColorPalette256FileSize) throw new InvalidDataException();

            var colorSize = bytesRead == ColorPalette16FileSize ? 16 : 256;
            var result = new Rgba32[colorSize];

            for (var i = 0; i < colorSize; i++)
            {
                var rawValue = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(i * 2, 2));
                result[i] = new Rgba32((byte) ((rawValue & 0x1F) << 3), (byte) (((rawValue >> 5) & 0x1F) << 3), (byte) (((rawValue >> 10) & 0x1F) << 3), (byte) (i == 0 ? 0 : 255));
            }

            return new ColorPalette(colorSize == 16 ? ColorPaletteType.Color16 : ColorPaletteType.Color256, result);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] GetBitmap(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public static ChunkBitmap GetChunkBitmap(ColorPalette colorPalette, Stream bitmap, Stream bitmapIndex)
    {
        var result = new ChunkBitmap();

        using (var reader = new BinaryReader(bitmap, Encoding.UTF8, true))
        {
            while (bitmap.Position < bitmap.Length)
            {
                var chunk = new byte[colorPalette.Type == ColorPaletteType.Color16 ? 4 * 8 : 8 * 8];

                for (var i = 0; i < chunk.Length; i++)
                {
                    chunk[i] = reader.ReadByte();
                }
                
                result.ColorPaletteIndexes.Add(chunk);
            }
        }

        using (var reader = new BinaryReader(bitmapIndex, Encoding.UTF8, true))
        {
            while (bitmapIndex.Position < bitmapIndex.Length)
            {
                result.BitmapIndices.Add(reader.ReadUInt16());
            }
        }

        return result;
    }
    
    public static SixLabors.ImageSharp.Image<Rgba32> GetImage(MpmHeader header, ColorPalette colorPalette, byte[] bitmap, int gridSize = 8)
    {
        var image = new SixLabors.ImageSharp.Image<Rgba32>(header.Width, header.Height);
        var bitmapIndex = 0;
        
        if (header.BitmapIndexFileIndex != 0)
        {
            var gridX = 0;
            var gridY = 0;
            
            if (colorPalette.Type == ColorPaletteType.Color16)
            {
                while (bitmapIndex * 2 < header.Width * header.Height)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x += 2)
                        {
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[bitmap[bitmapIndex] >> 4];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[bitmap[bitmapIndex] & 0xF];

                            bitmapIndex++;
                        }
                    }

                    gridX++;

                    if (gridX >= header.Width / gridSize)
                    {
                        gridX = 0;
                        gridY++;
                    }
                }
            }
            else if (colorPalette.Type == ColorPaletteType.Color256)
            {
                while (bitmapIndex < header.Width * header.Height)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x++)
                        {
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[bitmap[bitmapIndex++]];
                        }
                    }

                    gridX++;

                    if (gridX >= header.Width / gridSize)
                    {
                        gridX = 0;
                        gridY++;
                    }
                }
            }
        }
        else
        {
            if (colorPalette.Type == ColorPaletteType.Color16)
            {
                for (var y = 0; y < header.Height; y++)
                {
                    for (var x = 0; x < header.Width; x += 2)
                    {
                        image[x, y] = colorPalette.Table[bitmap[bitmapIndex] >> 4];
                        image[x + 1, y] = colorPalette.Table[bitmap[bitmapIndex] & 0xF];
                        bitmapIndex++;
                    }
                }
            }
            else if (colorPalette.Type == ColorPaletteType.Color256)
            {
                for (var y = 0; y < header.Height; y++)
                {
                    for (var x = 0; x < header.Width; x++)
                    {
                        image[x, y] = colorPalette.Table[bitmap[bitmapIndex]];
                        bitmapIndex++;
                    }
                }
            }
        }
        
        return image;
    }

    public static SixLabors.ImageSharp.Image<Rgba32> GetImage(MpmHeader header, ColorPalette colorPalette, ChunkBitmap chunkBitmap)
    {
        const int gridSize = 8;
        
        var image = new SixLabors.ImageSharp.Image<Rgba32>(header.Width, header.Height);
        var gridX = 0;
        var gridY = 0;

        foreach (var bitmapIndex in chunkBitmap.BitmapIndices)
        {
            var index = 0;

            if (colorPalette.Type == ColorPaletteType.Color16)
            {
                for (var y = 0; y < gridSize; y++)
                {
                    for (var x = 0; x < gridSize; x += 2)
                    {
                        if (bitmapIndex < chunkBitmap.ColorPaletteIndexes.Count)
                        {
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[chunkBitmap.ColorPaletteIndexes[bitmapIndex][index] >> 4];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[chunkBitmap.ColorPaletteIndexes[bitmapIndex][index] & 0xF];
                            index++;
                        }
                    }
                }
            }
            else
            {
                for (var y = 0; y < gridSize; y++)
                {
                    for (var x = 0; x < gridSize; x++)
                    {
                        if (bitmapIndex < chunkBitmap.ColorPaletteIndexes.Count)
                        {
                            var colorPaletteIndex = chunkBitmap.ColorPaletteIndexes[bitmapIndex][index];
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Table[colorPaletteIndex];
                            index++;
                        }
                    }
                }
            }
            
            gridX++;

            if (gridX >= header.Width / gridSize)
            {
                gridX = 0;
                gridY++;
            }
        }

        return image;
    }
}
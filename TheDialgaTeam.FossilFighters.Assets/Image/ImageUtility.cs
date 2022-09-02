using System.Buffers;
using System.Buffers.Binary;
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
    
    public static SixLabors.ImageSharp.Image<Rgba32> GetImage(MpmHeader header, ColorPalette colorPalette, byte[] bitmap, int gridSize = 8)
    {
        var image = new SixLabors.ImageSharp.Image<Rgba32>(header.Width, header.Height);
        var bitmapIndex = 0;
        
        if (header.Unknown7 != 0)
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
}
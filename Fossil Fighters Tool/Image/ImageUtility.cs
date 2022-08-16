using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool.Image;

public static class ImageUtility
{
    private const int ColorPalette16FileSize = 16 * 2;
    private const int ColorPalette256FileSize = 256 * 2;
    
    public static Rgba32[] GetColorPalette(Stream stream)
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

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] GetColorIndexes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }
}
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool.Motion;

public static class MotionUtility
{
    public static ColorPalette GetColorPalette(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var paletteType = (ColorPaletteType) reader.ReadInt32();
        var table = new Rgba32[paletteType == ColorPaletteType.Color16 ? 16 : 256];

        for (var i = 0; i < table.Length; i++)
        {
            var rawValue = reader.ReadUInt16();
            table[i] = new Rgba32((byte) ((rawValue & 0x1F) << 3), (byte) (((rawValue >> 5) & 0x1F) << 3), (byte) (((rawValue >> 10) & 0x1F) << 3), (byte) (i == 0 ? 0 : 255));
        }

        return new ColorPalette(paletteType, table);
    }

    public static Bitmap GetBitmap(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var rawData = reader.ReadInt32();

        int width;
        int height;

        switch (rawData & 0xF)
        {
            case 0x00:
                width = 8;
                height = 8;
                break;

            case 0x01:
                width = 16;
                height = 8;
                break;

            case 0x02:
                width = 8;
                height = 16;
                break;

            case 0x04:
                width = 16;
                height = 16;
                break;

            case 0x05:
                width = 32;
                height = 8;
                break;
            
            case 0x06:
                width = 8;
                height = 32;
                break;

            case 0x08:
                width = 32;
                height = 32;
                break;

            case 0x09:
                width = 32;
                height = 16;
                break;

            case 0x0A:
                width = 16;
                height = 32;
                break;

            case 0x0C:
                width = 64;
                height = 64;
                break;

            case 0x0D:
                width = 64;
                height = 32;
                break;

            case 0x0E:
                width = 32;
                height = 64;
                break;
            
            default:
                throw new InvalidDataException();
        }
        
        var colorPaletteType = (ColorPaletteType) ((rawData >> 16) & 0x1);
        var colorPaletteIndexes = new List<byte>();

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            colorPaletteIndexes.Add(reader.ReadByte());
        }

        return new Bitmap(width, height, colorPaletteType, colorPaletteIndexes.ToArray());
    }
}
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool.Motion;

public class ColorPaletteFileReader : IDisposable
{
    public enum ColorPaletteType
    {
        Color16 = 0,
        Color256 = 1
    }
    
    public ColorPaletteType ColorTableType { get; }
    
    public Rgba32[] ColorTable { get; }

    private readonly Stream _stream;

    public ColorPaletteFileReader(Stream stream)
    {
        _stream = stream;
        
        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);

        ColorTableType = Enum.Parse<ColorPaletteType>(binaryReader.ReadInt32().ToString());
        ColorTable = new Rgba32[ColorTableType == ColorPaletteType.Color16 ? 16 : 256];
        
        for (var i = 0; i < ColorTable.Length; i++)
        {
            var rawValue = binaryReader.ReadInt16();
            ColorTable[i] = new Rgba32((byte) ((rawValue & 0x1F) << 3), (byte) (((rawValue >> 5) & 0x1F) << 3), (byte) (((rawValue >> 10) & 0x1F) << 3), (byte) (i == 0 ? 0 : 255));
        }
    }
    
    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
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

    private readonly BinaryReader _reader;

    public ColorPaletteFileReader(BinaryReader reader)
    {
        _reader = reader;

        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        
        ColorTableType = (ColorPaletteType) reader.ReadInt32();
        ColorTable = new Rgba32[ColorTableType == ColorPaletteType.Color16 ? 16 : 256];
        
        for (var i = 0; i < ColorTable.Length; i++)
        {
            var rawValue = reader.ReadUInt16();
            ColorTable[i] = new Rgba32((byte) ((rawValue & 0x1F) << 3), (byte) (((rawValue >> 5) & 0x1F) << 3), (byte) (((rawValue >> 10) & 0x1F) << 3), (byte) (i == 0 ? 0 : 255));
        }
    }
    
    public void Dispose()
    {
        _reader.Dispose();
    }
}
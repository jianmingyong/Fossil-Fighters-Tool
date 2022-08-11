using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace Fossil_Fighters_Tool.Image;

public class ColorPaletteFileReader : IDisposable
{
    public Rgba32[] ColorTable { get; }

    private readonly Stream _stream;

    public ColorPaletteFileReader(Stream stream)
    {
        _stream = stream;
        
        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);
        
        ColorTable = new Rgba32[256];
        
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
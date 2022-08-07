using System.Text;

namespace Fossil_Fighters_Tool;

public class BitmapFileReader : IDisposable
{
    public int Unknown1 { get; }
    
    public int Width { get; }
    
    public int Height { get; }
    
    public int ColorType { get; }
    
    public byte[] BitmapColorIndexes { get; }

    private readonly Stream _stream;

    public BitmapFileReader(Stream stream)
    {
        _stream = stream;
        
        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);

        Unknown1 = binaryReader.ReadInt32();
        
        switch (Unknown1 & 0xF)
        {
            case 0x00:
                Width = 8;
                Height = 8;
                break;

            case 0x01:
                Width = 16;
                Height = 8;
                break;

            case 0x02:
                Width = 8;
                Height = 16;
                break;

            case 0x04:
                Width = 16;
                Height = 16;
                break;

            case 0x05:
                Width = 32;
                Height = 8;
                break;
            
            case 0x06:
                Width = 8;
                Height = 32;
                break;

            case 0x08:
                Width = 32;
                Height = 32;
                break;

            case 0x09:
                Width = 32;
                Height = 16;
                break;

            case 0x0A:
                Width = 16;
                Height = 32;
                break;

            case 0x0C:
                Width = 64;
                Height = 64;
                break;

            case 0x0D:
                Width = 64;
                Height = 32;
                break;

            case 0x0E:
                Width = 32;
                Height = 64;
                break;
        }

        ColorType = Unknown1 >> 16;

        var bitmapColorIndexes = new List<byte>();
        
        do
        {
            bitmapColorIndexes.Add(binaryReader.ReadByte());
        } while (_stream.Position < _stream.Length);

        BitmapColorIndexes = bitmapColorIndexes.ToArray();
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
using System.Text;

namespace Fossil_Fighters_Tool.Header;

public record MpmHeader
{
    public const int Id = 0x004D504D;
    
    public int Unknown1 { get; init; }
    
    public int Unknown2 { get; init; }
    
    public int Unknown3 { get; init; }
    
    public int Width { get; init; }
    
    public int Height { get; init; }
    
    public int Unknown4 { get; init; }
    
    public int Unknown5 { get; init; }
    
    public int Unknown6 { get; init; }
    
    public int ColorPaletteFileIndex { get; init; }

    public string ColorPaletteFileName { get; init; } = string.Empty;
    
    public int BitmapFileIndex { get; init; }
    
    public string BitmapFileName { get; init; }  = string.Empty;
    
    public int Unknown7 { get; init; }
    
    public string Unknown8 { get; init; }  = string.Empty;

    public static MpmHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var unknown1 = reader.ReadInt32();
        var unknown2 = reader.ReadInt32();
        var unknown3 = reader.ReadInt32();
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var unknown4 = reader.ReadInt32();
        var unknown5 = reader.ReadInt32();
        var unknown6 = reader.ReadInt32();
        var colorPaletteFileIndex = reader.ReadInt32();
        var colorPaletteFileOffset = reader.ReadInt32();
        var bitmapFileIndex = reader.ReadInt32();
        var bitmapFileOffset = reader.ReadInt32();
        var unknown7 = reader.ReadInt32();
        var unknown8 = reader.ReadInt32();

        var colorPaletteFile = new StringBuilder();
        reader.BaseStream.Seek(colorPaletteFileOffset, SeekOrigin.Begin);

        while (reader.PeekChar() != '\0')
        {
            colorPaletteFile.Append(reader.ReadChar());
        }

        var bitmapFile = new StringBuilder();
        reader.BaseStream.Seek(bitmapFileOffset, SeekOrigin.Begin);
        
        while (reader.PeekChar() != '\0')
        {
            bitmapFile.Append(reader.ReadChar());
        }
        
        var unknown8String = new StringBuilder();

        if (unknown8 != 0)
        {
            reader.BaseStream.Seek(bitmapFileOffset, SeekOrigin.Begin);
            
            while (reader.PeekChar() != '\0')
            {
                unknown8String.Append(reader.ReadChar());
            }
        }

        var header = new MpmHeader
        {
            Unknown1 = unknown1,
            Unknown2 = unknown2,
            Unknown3 = unknown3,
            Width = width,
            Height = height,
            Unknown4 = unknown4,
            Unknown5 = unknown5,
            Unknown6 = unknown6,
            ColorPaletteFileIndex = colorPaletteFileIndex,
            ColorPaletteFileName = colorPaletteFile.ToString(),
            BitmapFileIndex = bitmapFileIndex,
            BitmapFileName = bitmapFile.ToString(),
            Unknown7 = unknown7,
            Unknown8 = unknown8String.ToString()
        };

        return header;
    }
}
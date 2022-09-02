using System.Text;

namespace Fossil_Fighters_Tool.Header;

public class MmsFileReader : IDisposable
{
    public const int Id = 0x00534D4D;
    
    public int Unknown1 { get; }
    
    public int Unknown2 { get; }
    
    public int Unknown3 { get; }
    
    public int Unknown4 { get; }
    
    public int Unknown5 { get; }
    
    public int AnimationFileCount { get; }
    
    public int[] AnimationFileIndexes { get; }
    
    public string AnimationFileName { get; }
    
    public int ColorPaletteFileCount { get; }
    
    public int[] ColorPaletteFileIndexes { get; }
    
    public string ColorPaletteFileName { get; }
    
    public int BitmapFileCount { get; }

    public int[] BitmapFileIndexes { get; }
    
    public string BitmapFileName { get; }
    
    private readonly BinaryReader _reader;
    
    public MmsFileReader(BinaryReader reader)
    {
        _reader = reader;
        
        reader.BaseStream.Seek(0, SeekOrigin.Begin);
        
        if (reader.ReadInt32() != Id) throw new Exception("This is not a MMS file.");

        Unknown1 = reader.ReadInt32();
        Unknown2 = reader.ReadInt32();
        Unknown3 = reader.ReadInt32();
        Unknown4 = reader.ReadInt32();
        Unknown5 = reader.ReadInt32();
        
        AnimationFileCount = reader.ReadInt32();
        AnimationFileIndexes = new int[AnimationFileCount];
        var animationFileIndexOffset = reader.ReadInt32();
        var animationFileNameOffset = reader.ReadInt32();
        
        ColorPaletteFileCount = reader.ReadInt32();
        ColorPaletteFileIndexes = new int[ColorPaletteFileCount];
        var colorPaletteFileIndexOffset = reader.ReadInt32();
        var colorPaletteFileNameOffset = reader.ReadInt32();
        
        BitmapFileCount = reader.ReadInt32();
        BitmapFileIndexes = new int[BitmapFileCount];
        var bitmapFileIndexOffset = reader.ReadInt32();
        var bitmapFileNameOffset = reader.ReadInt32();

        reader.BaseStream.Seek(animationFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < AnimationFileCount; i++)
        {
            AnimationFileIndexes[i] = reader.ReadInt32();
        }
        
        reader.BaseStream.Seek(animationFileNameOffset, SeekOrigin.Begin);
        
        var animationFileName = new StringBuilder();
        char animationFileNameChar;
        
        while ((animationFileNameChar = reader.ReadChar()) != '\0')
        {
            animationFileName.Append(animationFileNameChar);
        }

        AnimationFileName = animationFileName.ToString();

        reader.BaseStream.Seek(colorPaletteFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < ColorPaletteFileCount; i++)
        {
            ColorPaletteFileIndexes[i] = reader.ReadInt32();
        }

        reader.BaseStream.Seek(colorPaletteFileNameOffset, SeekOrigin.Begin);

        var colorPaletteFileName = new StringBuilder();
        char colorPaletteFileNameChar;
        
        while ((colorPaletteFileNameChar = reader.ReadChar()) != '\0')
        {
            colorPaletteFileName.Append(colorPaletteFileNameChar);
        }

        ColorPaletteFileName = colorPaletteFileName.ToString();
        
        reader.BaseStream.Seek(bitmapFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < BitmapFileCount; i++)
        {
            BitmapFileIndexes[i] = reader.ReadInt32();
        }

        reader.BaseStream.Seek(bitmapFileNameOffset, SeekOrigin.Begin);

        var bitmapFileName = new StringBuilder();
        char bitmapFileNameChar;
        
        while ((bitmapFileNameChar = reader.ReadChar()) != '\0')
        {
            bitmapFileName.Append(bitmapFileNameChar);
        }

        BitmapFileName = bitmapFileName.ToString();
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
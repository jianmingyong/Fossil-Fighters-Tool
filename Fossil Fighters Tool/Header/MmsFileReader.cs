using System.Text;

namespace Fossil_Fighters_Tool.Header;

public class MmsFileReader : IDisposable
{
    private const int MmsFileHeaderId = 0x00534D4D;
    
    public int Unknown1 { get; }
    
    public int Unknown2 { get; }
    
    public int Unknown3 { get; }
    
    public int Unknown4 { get; }
    
    public int EndHeaderOffset { get; }
    
    public int AnimationFileCount { get; }
    
    public int[] AnimationFileIndexes { get; }
    
    public string AnimationFileName { get; }
    
    public int ColorPaletteFileCount { get; }
    
    public int[] ColorPaletteFileIndexes { get; }
    
    public string ColorPaletteFileName { get; }
    
    public int BitmapFileCount { get; }

    public int[] BitmapFileIndexes { get; }
    
    public string BitmapFileName { get; }

    private readonly FileStream _stream;

    public MmsFileReader(FileStream stream)
    {
        _stream = stream;

        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);
        
        if (binaryReader.ReadInt32() != MmsFileHeaderId)
        {
            throw new Exception("This is not a MMS file.");
        }

        Unknown1 = binaryReader.ReadInt32();
        Unknown2 = binaryReader.ReadInt32();
        Unknown3 = binaryReader.ReadInt32();
        Unknown4 = binaryReader.ReadInt32();
        EndHeaderOffset = binaryReader.ReadInt32();
        AnimationFileCount = binaryReader.ReadInt32();
        var animationFileIndexOffset = binaryReader.ReadInt32();
        var animationFileNameOffset = binaryReader.ReadInt32();
        ColorPaletteFileCount = binaryReader.ReadInt32();
        var colorPaletteFileIndexOffset = binaryReader.ReadInt32();
        var colorPaletteFileNameOffset = binaryReader.ReadInt32();
        BitmapFileCount = binaryReader.ReadInt32();
        var bitmapFileIndexOffset = binaryReader.ReadInt32();
        var bitmapFileNameOffset = binaryReader.ReadInt32();

        _stream.Seek(animationFileIndexOffset, SeekOrigin.Begin);

        var animationFileIndexes = new List<int>();
        
        do
        {
            animationFileIndexes.Add(binaryReader.ReadInt32());
        } while (_stream.Position < animationFileNameOffset);

        AnimationFileIndexes = animationFileIndexes.ToArray();
        
        _stream.Seek(animationFileNameOffset, SeekOrigin.Begin);
        
        var animationFileName = new StringBuilder();
        char animationFileNameChar;
        
        while ((animationFileNameChar = binaryReader.ReadChar()) != '\0')
        {
            animationFileName.Append(animationFileNameChar);
        }

        AnimationFileName = animationFileName.ToString();

        _stream.Seek(colorPaletteFileIndexOffset, SeekOrigin.Begin);

        var colorPaletteFileIndexes = new List<int>();

        do
        {
            colorPaletteFileIndexes.Add(binaryReader.ReadInt32());
        } while (_stream.Position < colorPaletteFileNameOffset);
        
        ColorPaletteFileIndexes = colorPaletteFileIndexes.ToArray();
        
        _stream.Seek(colorPaletteFileNameOffset, SeekOrigin.Begin);

        var colorPaletteFileName = new StringBuilder();
        char colorPaletteFileNameChar;
        
        while ((colorPaletteFileNameChar = binaryReader.ReadChar()) != '\0')
        {
            colorPaletteFileName.Append(colorPaletteFileNameChar);
        }

        ColorPaletteFileName = colorPaletteFileName.ToString();
        
        _stream.Seek(bitmapFileIndexOffset, SeekOrigin.Begin);

        var bitmapFileIndexes = new List<int>();

        do
        {
            bitmapFileIndexes.Add(binaryReader.ReadInt32());
        } while (_stream.Position < bitmapFileNameOffset);

        BitmapFileIndexes = bitmapFileIndexes.ToArray();
        
        _stream.Seek(bitmapFileNameOffset, SeekOrigin.Begin);

        var bitmapFileName = new StringBuilder();
        char bitmapFileNameChar;
        
        while ((bitmapFileNameChar = binaryReader.ReadChar()) != '\0')
        {
            bitmapFileName.Append(bitmapFileNameChar);
        }

        BitmapFileName = bitmapFileName.ToString();
    }
    
    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
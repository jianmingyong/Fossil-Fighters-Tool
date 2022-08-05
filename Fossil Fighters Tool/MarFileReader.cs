using System.Buffers;
using System.Text;

namespace Fossil_Fighters_Tool;

public class MarFileReader : IDisposable
{
    public readonly struct MarFilePointer
    {
        public int MarDataOffset { get; init; }
        
        public int McmDataSize { get; init; }
    }
    
    private const int MarFileHeaderId = 0x0052414D;
    
    public MarFilePointer[] FilePointers { get; }
    
    private readonly FileStream _stream;

    public MarFileReader(FileStream stream)
    {
        _stream = stream;

        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);

        if (binaryReader.ReadInt32() != MarFileHeaderId)
        {
            throw new Exception("This is not a MAR file.");
        }
        
        var filePointerCount = binaryReader.ReadInt32();
        FilePointers = new MarFilePointer[filePointerCount];

        for (var i = 0; i < filePointerCount; i++)
        {
            FilePointers[i] = new MarFilePointer
            {
                MarDataOffset = binaryReader.ReadInt32(),
                McmDataSize = binaryReader.ReadInt32()
            };
        }
    }

    public void ExtractTo(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        
        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        
        try
        {
            for (var i = 0; i < FilePointers.Length; i++)
            {
                var filePointer = FilePointers[i];
                _stream.Seek(filePointer.MarDataOffset, SeekOrigin.Begin);
                
                using var fileStream = new FileStream(Path.Combine(outputDirectory, $"{i}.mcm"), FileMode.Create);

                if (i + 1 < FilePointers.Length)
                {
                    var nextPointer = FilePointers[i + 1];
                    var fileLength = nextPointer.MarDataOffset - filePointer.MarDataOffset;
                    var written = 0;
                    
                    do
                    {
                        var readCount = _stream.Read(buffer, 0, Math.Min(4096, fileLength - written));
                        if (readCount == 0) throw new EndOfStreamException();
                    
                        fileStream.Write(buffer, 0, readCount);
                        fileStream.Flush();
            
                        written += readCount;
                    } while (written < fileLength);
                }
                else
                {
                    int readCount;
                    
                    while ((readCount = _stream.Read(buffer, 0, 4096)) > 0)
                    {
                        fileStream.Write(buffer, 0, readCount);
                        fileStream.Flush();
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
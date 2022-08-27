using System.Buffers;

namespace Fossil_Fighters_Tool.Archive;

public class MarArchiveEntry : IDisposable
{
    public int DataFileSize => _mcmFileStream?.DecompressFileSize ?? 0;
    
    internal readonly MemoryStream MemoryStream;
    
    private McmFileStream? _mcmFileStream;

    public MarArchiveEntry()
    {
        MemoryStream = new MemoryStream();
    }
    
    public MarArchiveEntry(BinaryReader reader, int fileOffset, int fileLength)
    {
        MemoryStream = new MemoryStream(Math.Max(0, fileLength));
        reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        var fileRemaining = fileLength;

        try
        {
            while (fileRemaining > 0)
            {
                var read = reader.Read(buffer, 0, 4096);
                if (read == 0) throw new EndOfStreamException();

                MemoryStream.Write(buffer, 0, read > fileRemaining ? fileRemaining : read);
                fileRemaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public McmFileStream OpenRead()
    {
        MemoryStream.Seek(0, SeekOrigin.Begin);
        _mcmFileStream = new McmFileStream(MemoryStream, McmFileStreamMode.Decompress, true);
        return _mcmFileStream;
    }
    
    public McmFileStream OpenWrite()
    {
        MemoryStream.Seek(0, SeekOrigin.Begin);
        MemoryStream.SetLength(0);
        _mcmFileStream = new McmFileStream(MemoryStream, McmFileStreamMode.Compress, true);
        return _mcmFileStream;
    }

    public void Dispose()
    {
        MemoryStream.Dispose();
    }
}
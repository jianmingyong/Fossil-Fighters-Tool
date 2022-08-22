using System.Buffers;

namespace Fossil_Fighters_Tool.Archive;

public class MarArchiveEntry
{
    private readonly MarArchive _archive;
    private readonly int _fileLength;
    private readonly int _fileOffset;

    public MarArchiveEntry(MarArchive archive, int fileOffset, int fileLength)
    {
        _archive = archive;
        _fileLength = fileLength;
        _fileOffset = fileOffset;
    }

    public Stream Open()
    {
        if (_archive.Mode == MarArchiveMode.Read)
        {
            if (_archive.ArchiveStream is MemoryStream memoryStream)
            {
                return new MemoryStream(memoryStream.GetBuffer(), _fileOffset, _fileLength);
            }

            var stream = new MemoryStream(_fileLength);
            var buffer = ArrayPool<byte>.Shared.Rent(_fileLength);

            try
            {
                _archive.ArchiveStream.Seek(_fileOffset, SeekOrigin.Begin);
                if (_archive.ArchiveStream.Read(buffer, 0, _fileLength) < _fileLength) throw new EndOfStreamException();
                stream.Write(buffer, 0, _fileLength);
                stream.Seek(0, SeekOrigin.Begin);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return stream;
        }
        
        throw new NotImplementedException();
    }
}
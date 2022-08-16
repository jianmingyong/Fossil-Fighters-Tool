namespace Fossil_Fighters_Tool.Archive;

public class MarArchiveEntry
{
    private readonly MarArchive _archive;
    private readonly long _fileLength;
    private readonly long _fileOffset;

    public MarArchiveEntry(MarArchive archive, long fileOffset, long fileLength)
    {
        _archive = archive;
        _fileLength = fileLength;
        _fileOffset = fileOffset;
    }

    public Stream Open()
    {
        if (_archive.Mode == MarArchiveMode.Read)
        {
            return new ReadOnlyStream(_archive.ArchiveStream, _fileOffset, _fileLength, true);
        }
        
        throw new NotImplementedException();
    }

    public void Delete()
    {
        if (_archive.Mode != MarArchiveMode.Update) throw new NotSupportedException();
        throw new NotImplementedException();
    }
}
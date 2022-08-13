namespace Fossil_Fighters_Tool.Archive;

public class MarArchiveEntry
{
    /*
        File Header
            0x00h 4     ID "MAR" (0x0052414D)
            0x04h 4     Number of files
            0x08h N*8   File Lists (see below)
            
        File Lists
            0x00h 4     MCM File offset (Offset from MAR+0)
            0x04h 4     Data File size (Decompressed)
     */

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
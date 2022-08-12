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
    
    public MarArchive Archive { get; }

    public long FileLength { get; }

    private readonly long _fileOffset;

    public MarArchiveEntry(MarArchive archive, long fileOffset, long fileLength)
    {
        Archive = archive;
        FileLength = fileLength;
        _fileOffset = fileOffset;
    }

    public Stream Open()
    {
        if (Archive.Mode == MarArchiveMode.Read)
        {
            return new ReadOnlyStream(Archive.ArchiveStream, _fileOffset, FileLength);
        }
        
        throw new NotImplementedException();
    }

    public void Delete()
    {
        if (Archive.Mode != MarArchiveMode.Update) throw new NotSupportedException();
        throw new NotImplementedException();
    }
}
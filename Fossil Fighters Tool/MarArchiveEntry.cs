using Fossil_Fighters_Tool.Archive;

namespace Fossil_Fighters_Tool;

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
    
    public long Length { get; }

    private long _fileOffset;

    public MarArchiveEntry(MarArchive archive, long fileOffset, long fileLength)
    {
        Archive = archive;
        _fileOffset = fileOffset;
        Length = fileLength;
    }

    public Stream Open()
    {
        if (Archive.Mode == MarArchiveMode.Read)
        {
            return new ReadOnlyStream(Archive._archiveStream, _fileOffset, Length);
        }
        
        throw new NotImplementedException();
    }

    public void Delete()
    {
        throw new NotImplementedException();
    }
}
using System.Text;

namespace Fossil_Fighters_Tool.Archive;

public class MarArchive : IDisposable
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
    
    private const int Id = 0x0052414D;
    
    public MarArchiveMode Mode { get; }

    public IReadOnlyList<MarArchiveEntry> Entries
    {
        get
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MarArchive));
            ReadEntries();
            return _entries;
        }
    }

    internal readonly Stream ArchiveStream;
    
    private readonly BinaryReader? _archiveReader;
    private readonly Stream _disposableStream;
    private readonly bool _leaveOpen;

    private readonly List<MarArchiveEntry> _entries = new();

    private bool _entriesRead;
    private bool _isDisposed;
    
    public MarArchive(Stream stream, MarArchiveMode mode = MarArchiveMode.Read, bool leaveOpen = false)
    {
        ArchiveStream = stream;
        _disposableStream = stream;
        _leaveOpen = leaveOpen;
        
        Mode = mode;
        
        try
        {
            switch (mode)
            {
                case MarArchiveMode.Read:
                    if (!stream.CanRead) throw new ArgumentException("Cannot use read mode on a non-readable stream.");
                    
                    if (!stream.CanSeek)
                    {
                        ArchiveStream = new MemoryStream();
                        stream.CopyTo(ArchiveStream);
                        ArchiveStream.Seek(0, SeekOrigin.Begin);
                    }
                    break;

                case MarArchiveMode.Create:
                    if (!stream.CanWrite) throw new ArgumentException("Cannot use create mode on a non-writable stream.");
                    _entriesRead = true;
                    break;

                case MarArchiveMode.Update:
                    if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek) throw new ArgumentException("Update mode requires a stream with read, write, and seek capabilities.");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            _archiveReader = mode == MarArchiveMode.Create ? null : new BinaryReader(ArchiveStream, Encoding.ASCII, leaveOpen);
            
            switch (mode)
            {
                case MarArchiveMode.Read:
                case MarArchiveMode.Update:
                    if (_archiveReader!.ReadUInt32() != Id) throw new InvalidDataException("The contents of the stream are not in the mar archive format.");
                    break;

                case MarArchiveMode.Create:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
        catch
        {
            if (ArchiveStream is MemoryStream) ArchiveStream.Dispose();
            throw;
        }
    }

    public MarArchiveEntry GetEntry(int entryIndex)
    {
        if (Mode == MarArchiveMode.Create) throw new NotSupportedException();
        
        ReadEntries();
        return _entries[entryIndex];
    }

    private void ReadEntries()
    {
        if (!_entriesRead)
        {
            _entriesRead = true;
            
            var expectedFileLength = _archiveReader!.ReadUInt32();
            var offsets = new List<(int offset, int dataSize)>();

            for (var i = 0; i < expectedFileLength; i++)
            {
                offsets.Add(((int) _archiveReader!.ReadUInt32(), (int) _archiveReader!.ReadUInt32()));
            }

            for (var i = 0; i < expectedFileLength; i++)
            {
                var endingOffset = i + 1 < expectedFileLength ? offsets[i + 1].offset : _archiveReader!.BaseStream.Length;
                _entries.Add(new MarArchiveEntry(this, offsets[i].offset, endingOffset - offsets[i].offset));
            }
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        if (!_leaveOpen)
        {
            _disposableStream.Dispose();
        }
        
        _archiveReader?.Dispose();
    }
}
using System.Diagnostics;
using System.Text;

namespace Fossil_Fighters_Tool.Archive;

public class MarArchive : IDisposable
{
    private const int Id = 0x0052414D;
    
    public MarArchiveMode Mode { get; }

    public IReadOnlyList<MarArchiveEntry> Entries
    {
        get
        {
            ReadEntries();
            return _entries;
        }
    }

    internal readonly Stream ArchiveStream;
    
    private readonly BinaryReader? _reader;
    private readonly Stream _disposableStream;
    private readonly bool _leaveOpen;

    private readonly List<MarArchiveEntry> _entries = new();

    private bool _entriesRead;
    
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

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            _reader = mode == MarArchiveMode.Create ? null : new BinaryReader(ArchiveStream, Encoding.UTF8, leaveOpen);
            
            switch (mode)
            {
                case MarArchiveMode.Read:
                    if (_reader!.ReadUInt32() != Id) throw new InvalidDataException(string.Format(Localization.StreamIsNotArchive, "MAR"));
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

    private void ReadEntries()
    {
        if (_entriesRead) return;
        _entriesRead = true;

        Debug.Assert(_reader != null, nameof(_reader) + " != null");
        
        var expectedFileLength = _reader.ReadInt32();
        var offsets = new List<(int offset, int dataSize)>();

        for (var i = 0; i < expectedFileLength; i++)
        {
            offsets.Add((_reader.ReadInt32(), _reader.ReadInt32()));
        }

        for (var i = 0; i < expectedFileLength; i++)
        {
            var endingOffset = i + 1 < expectedFileLength ? offsets[i + 1].offset : _reader.BaseStream.Length;
            _entries.Add(new MarArchiveEntry(this, offsets[i].offset, (int) (endingOffset - offsets[i].offset)));
        }
    }
    
    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _disposableStream.Dispose();
        }
        
        _reader?.Dispose();
    }
}
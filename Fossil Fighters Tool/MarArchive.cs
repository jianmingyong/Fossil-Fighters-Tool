using System.Collections.ObjectModel;
using System.Text;

namespace Fossil_Fighters_Tool;

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

    public IReadOnlyCollection<MarArchiveEntry> Entries
    {
        get
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MarArchive));
            EnsureReadEntries();
            return _entriesCollection;
        }
    }

    internal readonly Stream _archiveStream;
    private readonly Stream? _backingStream;
    private readonly bool _leaveOpen;

    private readonly BinaryReader? _archiveReader;

    private readonly List<MarArchiveEntry> _entries = new();
    private readonly ReadOnlyCollection<MarArchiveEntry> _entriesCollection;

    private bool _readEntries;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the MarArchive class on the specified stream for the specified mode, and optionally leaves the stream open.
    /// </summary>
    /// <param name="stream">The input or output stream.</param>
    /// <param name="mode">One of the enumeration values that indicates whether the mar archive is used to read, create, or update entries.</param>
    /// <param name="leaveOpen">true to leave the stream open after the MarArchive object is disposed; otherwise, false.</param>
    /// <exception cref="ArgumentException">The stream is already closed or does not support reading or writing.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">The contents of the stream are not in the mar archive format.</exception>
    public MarArchive(Stream stream, MarArchiveMode mode = MarArchiveMode.Read, bool leaveOpen = false)
    {
        _archiveStream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
        _entriesCollection = new ReadOnlyCollection<MarArchiveEntry>(_entries);
        Mode = mode;
        
        try
        {
            switch (mode)
            {
                case MarArchiveMode.Read:
                    if (!stream.CanRead) throw new ArgumentException("Cannot use read mode on a non-readable stream.");
                    
                    if (!stream.CanSeek)
                    {
                        _archiveStream = _backingStream = new MemoryStream();
                        stream.CopyTo(_backingStream);
                    }
                    break;

                case MarArchiveMode.Create:
                    if (!stream.CanWrite) throw new ArgumentException("Cannot use create mode on a non-writable stream.");
                    _readEntries = true;
                    break;

                case MarArchiveMode.Update:
                    if (!stream.CanRead || !stream.CanWrite || !stream.CanSeek) throw new ArgumentException("Update mode requires a stream with read, write, and seek capabilities.");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            _archiveReader = mode == MarArchiveMode.Create ? null : new BinaryReader(_archiveStream, Encoding.ASCII, leaveOpen);
            
            switch (mode)
            {
                case MarArchiveMode.Read:
                case MarArchiveMode.Update:
                    if (_archiveReader!.ReadInt32() != Id) throw new InvalidDataException("The contents of the stream are not in the mar archive format.");
                    break;

                case MarArchiveMode.Create:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
        catch
        {
            _backingStream?.Dispose();
            throw;
        }
    }

    private void EnsureReadEntries()
    {
        if (!_readEntries)
        {
            _readEntries = true;

            _archiveReader!.BaseStream.Seek(0x4, SeekOrigin.Begin);
            var expectedFileLength = _archiveReader!.ReadInt32();
            var offsets = new List<(int offset, int dataSize)>();

            for (var i = 0; i < expectedFileLength; i++)
            {
                offsets.Add((_archiveReader!.ReadInt32(), _archiveReader!.ReadInt32()));
            }

            for (var i = 0; i < expectedFileLength; i++)
            {
                var endingOffset = i + 1 < expectedFileLength ? offsets[i + 1].offset : _archiveReader.BaseStream.Length;
                _entries.Add(new MarArchiveEntry(this, offsets[i].offset, endingOffset - offsets[i].offset));
            }
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        if (_leaveOpen)
        {
            _backingStream?.Dispose();
        }
        else
        {
            _archiveStream.Dispose();
        }
    }
}
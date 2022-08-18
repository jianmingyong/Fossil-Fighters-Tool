using System.Buffers;
using Fossil_Fighters_Tool.Archive.Compression.Huffman;
using Fossil_Fighters_Tool.Archive.Compression.Lzss;
using Fossil_Fighters_Tool.Archive.Compression.Rle;

namespace Fossil_Fighters_Tool.Archive;

public class McmFileStream : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private const int Id = 0x004D434D;
    
    private readonly Stream _outputStream;
    private readonly McmFileStreamMode _mode;
    private readonly bool _leaveOpen;

    // Shared between decompress and compress
    private IMemoryOwner<byte> _unusedMemoryOwner;
    private ReadOnlyMemory<byte> _unusedBuffer = ReadOnlyMemory<byte>.Empty;

    // Decompress
    private bool _hasFirstHeaderChunk;
    private bool _hasSecondHeaderChunk;
    private int _decompressionSize;
    private int _maxSizePerChunk;
    private McmCompressionType _decompressionType1;
    private McmCompressionType _decompressionType2;
    private int[] _dataChunkOffsets = Array.Empty<int>();
    private int _dataChunkIndex;
    
    // Compress
    private MemoryStream _compressDataBuffer = new();
    private int _originalFileSize;
    private int _numberOfChunks;

    public McmFileStream(Stream outputStream, McmFileStreamMode mode, bool leaveOpen = false)
    {
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
        _unusedMemoryOwner = MemoryPool<byte>.Shared.Rent(20);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var firstSegment = new ReadOnlyMemorySegment<byte>(_unusedBuffer);
        var lastSegment = firstSegment.Add(new ReadOnlyMemory<byte>(buffer, offset, count));
        var sequenceReader = new SequenceReader<byte>(new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length));

        if (_mode == McmFileStreamMode.Decompress)
        {
            Decompress(ref sequenceReader);
        }
        else
        {
            Compress(ref sequenceReader);
        }
        
        var unreadSequence = sequenceReader.UnreadSequence;
        unreadSequence.CopyTo(_unusedMemoryOwner.Memory.Span);
        _unusedBuffer = _unusedMemoryOwner.Memory[..(int) unreadSequence.Length];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        _outputStream.Flush();
    }
    
    private void Compress(ref SequenceReader<byte> sequenceReader)
    {
        
    }

    private void Decompress(ref SequenceReader<byte> reader)
    {
        if (!_hasFirstHeaderChunk)
        {
            if (reader.Length - reader.Consumed < 20) return;

            reader.TryReadLittleEndian(out int rawData);
            
            if (rawData != Id) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressed, "MCM"));

            reader.TryReadLittleEndian(out _decompressionSize);
            reader.TryReadLittleEndian(out _maxSizePerChunk);
            reader.TryReadLittleEndian(out int dataChunkCount);

            _dataChunkOffsets = new int[dataChunkCount + 1];

            reader.TryRead(out var decompressionType1);
            reader.TryRead(out var decompressionType2);
            
            _decompressionType1 = (McmCompressionType) decompressionType1;
            _decompressionType2 = (McmCompressionType) decompressionType2;
            
            reader.Advance(2);
            
            _unusedMemoryOwner.Dispose();
            _unusedMemoryOwner = MemoryPool<byte>.Shared.Rent(_maxSizePerChunk);

            _dataChunkIndex = 0;

            _hasFirstHeaderChunk = true;
        }

        if (_hasFirstHeaderChunk && !_hasSecondHeaderChunk)
        {
            for (; _dataChunkIndex < _dataChunkOffsets.Length; _dataChunkIndex++)
            {
                if (!reader.TryReadLittleEndian(out _dataChunkOffsets[_dataChunkIndex])) return;
            }

            _dataChunkIndex = 0;

            _hasSecondHeaderChunk = true;
        }

        if (_hasFirstHeaderChunk && _hasSecondHeaderChunk)
        {
            for (; _dataChunkIndex < _dataChunkOffsets.Length - 1; _dataChunkIndex++)
            {
                var requiredLength = _dataChunkOffsets[_dataChunkIndex + 1] - _dataChunkOffsets[_dataChunkIndex];
                if (reader.Length - reader.Consumed < requiredLength) return;
                
                Stream decompressStream;
                var disposable = true;
                
                switch (_decompressionType2)
                {
                    case McmCompressionType.None:
                    {
                        decompressStream = _outputStream;
                        disposable = false;
                        break;
                    }

                    case McmCompressionType.Rle:
                    {
                        decompressStream = new RleStream(_outputStream, RleStreamMode.Decompress, _maxSizePerChunk, true);
                        disposable = true;
                        break;
                    }

                    case McmCompressionType.Lzss:
                    {
                        decompressStream = new LzssStream(_outputStream, LzssStreamMode.Decompress, _maxSizePerChunk, true);
                        disposable = true;
                        break;
                    }

                    case McmCompressionType.Huffman:
                    {
                        decompressStream = new HuffmanStream(_outputStream, HuffmanStreamMode.Decompress, _maxSizePerChunk, true);
                        disposable = true;
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                switch (_decompressionType1)
                {
                    case McmCompressionType.None:
                    {
                        decompressStream = _outputStream;
                        disposable = false;
                        break;
                    }
                    
                    case McmCompressionType.Rle:
                    {
                        decompressStream = new RleStream(decompressStream, RleStreamMode.Decompress, _maxSizePerChunk, !disposable);
                        disposable = true;
                        break;
                    }

                    case McmCompressionType.Lzss:
                    {
                        decompressStream = new LzssStream(decompressStream, LzssStreamMode.Decompress, _maxSizePerChunk, !disposable);
                        disposable = true;
                        break;
                    }

                    case McmCompressionType.Huffman:
                    {
                        decompressStream = new HuffmanStream(decompressStream, HuffmanStreamMode.Decompress, _maxSizePerChunk, !disposable);
                        disposable = true;
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                var sliceToCopy = reader.UnreadSequence.Slice(0, requiredLength);
                var buffer = ArrayPool<byte>.Shared.Rent(requiredLength);

                try
                {
                    sliceToCopy.CopyTo(buffer);
                    decompressStream.Write(buffer, 0, requiredLength);
                    reader.Advance(requiredLength);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (disposable)
                {
                    decompressStream.Dispose();
                }
            }
            
            _outputStream.Flush();

            if (_outputStream.Length != _decompressionSize) throw new InvalidDataException(Localization.StreamIsCorrupted);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_leaveOpen)
            {
                _outputStream.Dispose();
            }

            _unusedMemoryOwner.Dispose();
        }

        base.Dispose(disposing);
    }
}
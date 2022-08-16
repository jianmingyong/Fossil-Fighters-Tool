using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanStream : Stream
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

    public HuffmanDataSize DataSize
    {
        get => _dataSize;
        set
        {
            if (_streamMode == HuffmanStreamMode.Decompress) throw new NotSupportedException();
            _dataSize = value;
        }
    }

    private readonly Stream _outputStream;
    private readonly HuffmanStreamMode _streamMode;
    private readonly bool _leaveOpen;
    
    // Shared between decompress and compress
    private readonly IMemoryOwner<byte> _unusedMemoryOwner;
    private ReadOnlyMemory<byte> _unusedBuffer = ReadOnlyMemory<byte>.Empty;
    
    private HuffmanDataSize _dataSize;
    
    // Decompress
    private bool _hasDataHeader;
    private bool _hasTreeSize;
    private bool _hasTreeBuilt;
    private int _decompressLength;
    private int _treeSize;
    private int _treeNodeLength;
    private HuffmanNode? _rootNode;
    private HuffmanNode? _currentNode;
    private bool _isHalfDataWritten;
    private byte _halfData;
    private int _bytesWritten;

    public HuffmanStream(Stream outputStream, HuffmanStreamMode streamMode, int maxSizePerChunk, bool leaveOpen = false)
    {
        _outputStream = outputStream;
        _streamMode = streamMode;
        _leaveOpen = leaveOpen;
        _unusedMemoryOwner = MemoryPool<byte>.Shared.Rent(maxSizePerChunk);
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        var firstSegment = new ReadOnlyMemorySegment<byte>(_unusedBuffer);
        var lastSegment = firstSegment.Append(new ReadOnlyMemory<byte>(buffer, offset, count));
        var sequenceReader = new SequenceReader<byte>(new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length));

        if (_streamMode == HuffmanStreamMode.Decompress)
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
    
    private void Compress(ref SequenceReader<byte> reader)
    {
        throw new NotImplementedException();
    }

    private void Decompress(ref SequenceReader<byte> reader)
    {
        if (!_hasDataHeader)
        {
            if (!reader.TryReadLittleEndian(out int rawData)) return;
            if (((rawData >> 4) & 0xF) != 0x2) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressed, "Huffman"));
                
            _dataSize = (HuffmanDataSize) (rawData & 0xF);
            _decompressLength = (rawData >> 8) & 0xFFFFFF;
            
            _hasDataHeader = true;
        }

        if (_hasDataHeader && !_hasTreeSize)
        {
            if (!reader.TryRead(out var rawData)) return;

            _treeSize = rawData;
            _treeNodeLength = (_treeSize + 1) * 2 - 1;

            _hasTreeSize = true;
        }

        if (_hasDataHeader && _hasTreeSize && !_hasTreeBuilt)
        {
            if (reader.Length - reader.Consumed < _treeNodeLength) return;
            
            _rootNode = new HuffmanNode(reader, reader.Consumed, _dataSize, false);
            _currentNode = _rootNode;
            
            reader.Advance(_treeNodeLength);

            _hasTreeBuilt = true;
        }

        if (_hasDataHeader && _hasTreeSize && _hasTreeBuilt)
        {
            while (_bytesWritten < _decompressLength)
            {
                if (!reader.TryReadLittleEndian(out int bitStream)) return;

                for (var index = 31; index >= 0; index--)
                {
                    var direction = (bitStream >> index) & 0x01;
                    
                    if (direction == 0)
                    {
                        _currentNode = _currentNode!.Left ?? throw new InvalidDataException("The contents of the stream contains invalid bitstream.");
                        if (!_currentNode.Data.HasValue) continue;
                    }
                    else
                    {
                        _currentNode = _currentNode!.Right ?? throw new InvalidDataException("The contents of the stream contains invalid bitstream.");
                        if (!_currentNode.Data.HasValue) continue;
                    }
                    
                    if (_dataSize == HuffmanDataSize.FourBits)
                    {
                        if (_isHalfDataWritten)
                        {
                            var buffer = ArrayPool<byte>.Shared.Rent(1);

                            try
                            {
                                buffer[0] = (byte) (_halfData | (_currentNode.Data.Value << 4));
                                _outputStream.Write(buffer, 0, 1);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                            
                            _bytesWritten += 1;
                            _isHalfDataWritten = false;
                        }
                        else
                        {
                            _halfData = _currentNode.Data.Value;
                            _isHalfDataWritten = true;
                        }
                            
                        _currentNode = _rootNode;
                    }
                    else
                    {
                        var buffer = ArrayPool<byte>.Shared.Rent(1);

                        try
                        {
                            buffer[0] = _currentNode.Data.Value;
                            _outputStream.Write(buffer, 0, 1);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                        
                        _bytesWritten += 1;
                        _currentNode = _rootNode;
                    }
                    
                    if (_bytesWritten == _decompressLength) break;
                }
            }
            
            _outputStream.Flush();
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
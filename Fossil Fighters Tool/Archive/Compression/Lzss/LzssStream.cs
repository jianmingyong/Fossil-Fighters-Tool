using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Lzss;

public class LzssStream : Stream
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

    private const int LookbackBufferSize = 0xFFF + 1;
    
    private readonly Stream _outputStream;
    private readonly LzssStreamMode _mode;
    private readonly bool _leaveOpen;
    
    // Shared between decompress and compress
    private readonly IMemoryOwner<byte> _unusedMemoryOwner;
    private readonly ReadOnlyMemorySegment<byte> _unusedSegment;
    private readonly ReadOnlyMemorySegment<byte> _incomingSegment;
    
    // Decompress
    private bool _hasHeader;
    private bool _hasFlag;
    private int _decompressSize;
    private int _bytesWritten;
    private byte[] _lookbackBuffer;
    private int _lookbackIndex;
    private byte _flagData;
    private int _blockIndex;
    
    public LzssStream(Stream outputStream, LzssStreamMode mode, int maxSizePerChunk, bool leaveOpen = false)
    {
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
        _unusedMemoryOwner = MemoryPool<byte>.Shared.Rent(maxSizePerChunk);
        _unusedSegment = new ReadOnlyMemorySegment<byte>(ReadOnlyMemory<byte>.Empty);
        _incomingSegment = _unusedSegment.Add(ReadOnlyMemory<byte>.Empty);
        _lookbackBuffer = mode == LzssStreamMode.Decompress ? ArrayPool<byte>.Shared.Rent(LookbackBufferSize) : Array.Empty<byte>();
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _incomingSegment.Update(new ReadOnlyMemory<byte>(buffer, offset, count));
        var sequenceReader = new SequenceReader<byte>(new ReadOnlySequence<byte>(_unusedSegment, 0, _incomingSegment, _incomingSegment.Memory.Length));

        if (_mode == LzssStreamMode.Decompress)
        {
            Decompress(ref sequenceReader);
            if (_decompressSize > 0 && _bytesWritten >= _decompressSize) return;
        }
        else
        {
            Compress(ref sequenceReader);
        }
        
        var unreadSequence = sequenceReader.UnreadSequence;
        unreadSequence.CopyTo(_unusedMemoryOwner.Memory.Span);
        _unusedSegment.Update(_unusedMemoryOwner.Memory[..(int) unreadSequence.Length]);
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
        if (!_hasHeader)
        {
            if (!reader.TryReadLittleEndian(out int rawData)) return;
            if (((rawData >> 4) & 0xF) != 0x1) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressed, "LZSS"));

            _decompressSize = (rawData >> 8) & 0xFFFFFF;
            _lookbackIndex = 0;

            _hasHeader = true;
        }

        if (_hasHeader)
        {
            while (_bytesWritten < _decompressSize)
            {
                if (!_hasFlag)
                {
                    if (!reader.TryRead(out _flagData)) return;
                    
                    _blockIndex = 7;
                    
                    _hasFlag = true;
                }

                if (_hasFlag)
                {
                    for (; _blockIndex >= 0; _blockIndex--)
                    {
                        var blockType = (_flagData >> _blockIndex) & 0x1;

                        if (blockType == 0)
                        {
                            if (!reader.TryRead(out var rawData)) return;
                            
                            _outputStream.WriteByte(rawData);
                        
                            _lookbackBuffer[_lookbackIndex++] = rawData;
                            if (_lookbackIndex >= LookbackBufferSize) _lookbackIndex = 0;
                        
                            _bytesWritten += 1;
                        }
                        else
                        {
                            if (!reader.TryReadLittleEndian(out short rawData)) return;
                            
                            var bytesToCopy = ((rawData >> 4) & 0xF) + 3;
                            var offset = (((rawData & 0xF) << 8) | ((rawData >> 8) & 0xFF)) + 1;
                            
                            for (var i = 0; i < bytesToCopy; i++)
                            {
                                var index = _lookbackIndex - offset;

                                switch (index)
                                {
                                    case < 0:
                                        index += LookbackBufferSize;
                                        break;

                                    case >= LookbackBufferSize:
                                        index -= LookbackBufferSize;
                                        break;
                                }
                            
                                _outputStream.WriteByte(_lookbackBuffer[index]);
                                _bytesWritten += 1;
                            
                                _lookbackBuffer[_lookbackIndex++] = _lookbackBuffer[index];
                                if (_lookbackIndex >= LookbackBufferSize) _lookbackIndex = 0;
                            }
                        }
                        
                        if (_bytesWritten == _decompressSize) break;
                    }
                    
                    _hasFlag = false;
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

            if (_lookbackBuffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_lookbackBuffer);
            }
        }

        base.Dispose(disposing);
    }
}
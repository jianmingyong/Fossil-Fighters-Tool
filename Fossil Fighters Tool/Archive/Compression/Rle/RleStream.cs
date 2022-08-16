using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Rle;

public class RleStream : Stream
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
    
    private readonly Stream _outputStream;
    private readonly RleStreamMode _mode;
    private readonly bool _leaveOpen;

    // Shared between decompress and compress
    private readonly IMemoryOwner<byte> _unusedMemoryOwner;
    private ReadOnlyMemory<byte> _unusedBuffer = ReadOnlyMemory<byte>.Empty;
    
    // Decompress
    private bool _hasHeader;
    private bool _hasFlag;
    private int _decompressSize;
    private int _bytesWritten;
    private byte _flagDataLength;
    private byte _flagType;

    public RleStream(Stream outputStream, RleStreamMode mode, int maxSizePerChunk, bool leaveOpen = false)
    {
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
        _unusedMemoryOwner = MemoryPool<byte>.Shared.Rent(maxSizePerChunk);
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        var firstSegment = new ReadOnlyMemorySegment<byte>(_unusedBuffer);
        var lastSegment = firstSegment.Append(new ReadOnlyMemory<byte>(buffer, offset, count));
        var sequenceReader = new SequenceReader<byte>(new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length));

        if (_mode == RleStreamMode.Decompress)
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
        if (!_hasHeader)
        {
            if (!reader.TryReadLittleEndian(out int rawData)) return;
            if (((rawData >> 4) & 0xF) != 0x3) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressed, "RLE"));

            _decompressSize = (rawData >> 8) & 0xFFFFFF;
            
            _hasHeader = true;
        }

        if (_hasHeader)
        {
            while (_bytesWritten < _decompressSize)
            {
                if (!_hasFlag)
                {
                    if (!reader.TryRead(out var rawData)) return;

                    _flagDataLength = (byte) (rawData & 0x7F);
                    _flagType = (byte) (rawData >> 7);
                   
                    _hasFlag = true;
                }

                if (_hasFlag)
                {
                    if (_flagType == 0)
                    {
                        var bytesToRead = _flagDataLength + 1;
                        var buffer = ArrayPool<byte>.Shared.Rent(bytesToRead);

                        try
                        {
                            if (!reader.TryCopyTo(buffer.AsSpan(0, bytesToRead))) return;
                            reader.Advance(bytesToRead);
                            
                            _outputStream.Write(buffer, 0, bytesToRead);
                            _bytesWritten += bytesToRead;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                    else
                    {
                        if (!reader.TryRead(out var rawData)) return;
                        
                        for (var i = 0; i < _flagDataLength + 3; i++)
                        {
                            _outputStream.WriteByte(rawData);
                        }

                        _bytesWritten += _flagDataLength + 3;
                    }
                    
                    _hasFlag = false;
                }
            }
            
            _outputStream.Flush();
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (!_leaveOpen)
        {
            _outputStream.Dispose();
        }
        
        _unusedMemoryOwner.Dispose();
        
        base.Dispose(disposing);
    }
}
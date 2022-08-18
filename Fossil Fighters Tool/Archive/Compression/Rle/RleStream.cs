using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

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
    private readonly ReadOnlyMemorySegment<byte> _unusedSegment;
    private readonly ReadOnlyMemorySegment<byte> _incomingSegment;

    // Compress
    private const int MaxRawDataLength = (1 << 7) - 1 + 1;
    private const int MinCompressDataLength = 3;
    private const int MaxCompressDataLength = (1 << 7) - 1 + 3;

    private int _decompressDataLength;
    private MemoryStream? _compressData;
    private byte[]? _flagDataBuffer;
    private int _flagCount;

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
        _unusedSegment = new ReadOnlyMemorySegment<byte>(ReadOnlyMemory<byte>.Empty);
        _incomingSegment = _unusedSegment.Add(ReadOnlyMemory<byte>.Empty);

        if (mode == RleStreamMode.Compress)
        {
            _compressData = new MemoryStream();
            _flagDataBuffer = ArrayPool<byte>.Shared.Rent(maxSizePerChunk);
        }
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _incomingSegment.Update(new ReadOnlyMemory<byte>(buffer, offset, count));
        var sequenceReader = new SequenceReader<byte>(new ReadOnlySequence<byte>(_unusedSegment, 0, _incomingSegment, _incomingSegment.Memory.Length));

        if (_mode == RleStreamMode.Decompress)
        {
            Decompress(ref sequenceReader);
        }
        else
        {
            _decompressDataLength += count;
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
        if (_mode == RleStreamMode.Compress)
        {
            Debug.Assert(_compressData != null, nameof(_compressData) + " != null");

            if (_unusedSegment.Memory.Length > 0)
            {
                Compress(new SequenceReader<byte>(new ReadOnlySequence<byte>(_unusedSegment.Memory)));
            
                var temp = ArrayPool<byte>.Shared.Rent(4);
            
                try
                {
                    BinaryPrimitives.WriteInt32LittleEndian(temp.AsSpan(0, 4), (3 << 4) | (_decompressDataLength << 8));
                    _outputStream.Write(temp, 0, 4);
                    _compressData.Seek(0, SeekOrigin.Begin);
                    _compressData.CopyTo(_outputStream);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(temp);
                }
            }
            
            _unusedSegment.Update(ReadOnlyMemory<byte>.Empty);
            _incomingSegment.Update(ReadOnlyMemory<byte>.Empty);
            _compressData.Seek(0, SeekOrigin.Begin);
            _compressData.SetLength(0);
        }
        
        _outputStream.Flush();
    }

    private void Compress(SequenceReader<byte> reader)
    {
        Debug.Assert(_compressData != null, nameof(_compressData) + " != null");
        Debug.Assert(_flagDataBuffer != null, nameof(_flagDataBuffer) + " != null");

        int RepeatCount(byte compareData, SequenceReader<byte> temp)
        {
            var count = 0;
            
            while (temp.TryRead(out var nextData) && compareData == nextData)
            {
                count++;
            }
            
            return count;
        }

        void WriteCompressed(byte data, int count)
        {
            Debug.Assert(_compressData != null, nameof(_compressData) + " != null");
            
            _compressData.WriteByte((byte) ((1 << 7) + count - 3));
            _compressData.WriteByte(data);
        }

        void WriteUncompressed(byte[] data, int offset, int length)
        {
            Debug.Assert(_compressData != null, nameof(_compressData) + " != null");
            
            _compressData.WriteByte((byte) (length - 1));
            _compressData.Write(data, offset, length);
        }
        
        while (reader.TryRead(out var rawData))
        {
            var repeatCount = RepeatCount(rawData, reader) + 1;

            if (repeatCount >= MinCompressDataLength)
            {
                reader.Advance(repeatCount - 1);

                for (var i = 0; i < _flagCount; i += MaxRawDataLength)
                {
                    WriteUncompressed(_flagDataBuffer, i, Math.Min(_flagCount - i, MaxRawDataLength));
                }

                _flagCount = 0;
                
                while (repeatCount >= MinCompressDataLength)
                {
                    var temp = Math.Min(repeatCount, MaxCompressDataLength);
                    WriteCompressed(rawData, temp);
                    repeatCount -= temp;
                }
                    
                _flagDataBuffer.AsSpan(_flagCount, repeatCount).Fill(rawData);
                _flagCount += repeatCount;
            }
            else
            {
                _flagDataBuffer[_flagCount++] = rawData;
                repeatCount -= 1;
                    
                reader.TryCopyTo(_flagDataBuffer.AsSpan(_flagCount, repeatCount));
                reader.Advance(repeatCount);
                _flagCount += repeatCount;
            }
        }
        
        for (var i = 0; i < _flagCount; i += MaxRawDataLength)
        {
            WriteUncompressed(_flagDataBuffer, i, Math.Min(_flagCount - i, MaxRawDataLength));
        }

        _flagCount = 0;
    }
    
    private void Decompress(ref SequenceReader<byte> reader)
    {
        if (!_hasHeader)
        {
            if (!reader.TryReadLittleEndian(out int rawData)) return;
            if ((rawData & 0x30) != 0x30) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressed, "RLE"));

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
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                        
                        _bytesWritten += bytesToRead;
                    }
                    else
                    {
                        var bufferLength = _flagDataLength + 3;
                        var buffer = ArrayPool<byte>.Shared.Rent(bufferLength);

                        try
                        {
                            if (!reader.TryRead(out var rawData)) return;
                            buffer.AsSpan(0, bufferLength).Fill(rawData);
                            _outputStream.Write(buffer, 0, bufferLength);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                        
                        _bytesWritten += bufferLength;
                    }
                    
                    _hasFlag = false;
                }
            }
            
            _outputStream.Flush();
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        Flush();
        
        if (!_leaveOpen)
        {
            _outputStream.Dispose();
        }
        
        _unusedMemoryOwner.Dispose();
        _unusedSegment.Update(ReadOnlyMemory<byte>.Empty);
        _incomingSegment.Update(ReadOnlyMemory<byte>.Empty);

        if (_mode == RleStreamMode.Compress)
        {
            Debug.Assert(_compressData != null, nameof(_compressData) + " != null");
            Debug.Assert(_flagDataBuffer != null, nameof(_flagDataBuffer) + " != null");
            
            _compressData.Dispose();
            ArrayPool<byte>.Shared.Return(_flagDataBuffer);
        }
        
        base.Dispose(disposing);
    }
}
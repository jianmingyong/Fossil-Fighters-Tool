using System.Buffers;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace Fossil_Fighters_Tool.Archive.Compression.Lzss;

public class LzssStream : Stream
{
    public override bool CanRead => Mode == LzssStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == LzssStreamMode.Compress && BaseStream.CanWrite;
    
    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    [PublicAPI]
    public Stream BaseStream { get; }
    
    [PublicAPI]
    public LzssStreamMode Mode { get; }

    private const int MinDisplacement = 0x1 + 1;
    private const int MaxDisplacement = 0xFFF + 1;
    
    private const int MinBytesToCopy = 3;
    private const int MaxBytesToCopy = 0xF + 3;
    
    private MemoryStream? _inputStream;
    private MemoryStream? _outputStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    private bool _hasDecompressed;
    private bool _hasCompressed;

    public LzssStream(Stream stream, LzssStreamMode mode, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;
        
        if (mode == LzssStreamMode.Decompress)
        {
            _outputStream = new MemoryStream();
            _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen);
            _writer = new BinaryWriter(_outputStream);
        }
        else
        {
            _inputStream = new MemoryStream();
            _reader = new BinaryReader(_inputStream);
            _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);
        }
    }
    
    public override int ReadByte()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1);

        try
        {
            return Read(buffer, 0, 1) > 0 ? buffer[0] : -1;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        Debug.Assert(_outputStream != null, nameof(_outputStream) + " != null");

        if (!_hasDecompressed)
        {
            var rawHeaderData = _reader.ReadInt32();
            if ((rawHeaderData & 0x10) != 0x10) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "LZSS"));

            var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;
            _outputStream.Capacity = decompressSize;
            
            while (_outputStream.Length < decompressSize)
            {
                var flagData = _reader.ReadByte();

                for (var i = 7; i >= 0; i--)
                {
                    var blockType = (flagData >> i) & 0x1;

                    if (blockType == 0)
                    {
                        _writer.Write(_reader.ReadByte());
                    }
                    else
                    {
                        var compressHeader = _reader.ReadInt16();
                        var copyCount = ((compressHeader >> 4) & 0xF) + 3;
                        var displacement = (((compressHeader & 0xF) << 8) | ((compressHeader >> 8) & 0xFF)) + 1;
                        var lookbackBuffer = _outputStream.GetBuffer().AsSpan((int) (_outputStream.Position - displacement));

                        for (var j = 0; j < copyCount; j++)
                        {
                            _writer.Write(lookbackBuffer[j]);
                        }
                    }
                    
                    if (_outputStream.Length >= decompressSize) break;
                }
            }
            
            _outputStream.Seek(0, SeekOrigin.Begin);
            _hasDecompressed = true;
        }

        return _outputStream.Read(buffer, offset, count);
    }
    
    public override void WriteByte(byte value)
    {
        Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
        
        if (_hasCompressed) throw new IOException(Localization.StreamIsAlreadyCompressed);
        
        _inputStream.WriteByte(value);
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");

        if (_hasCompressed) throw new IOException(Localization.StreamIsAlreadyCompressed);
        
        _inputStream.Write(buffer, offset, count);
    }
    
    public override void Flush()
    {
        Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
        
        if (_hasCompressed) return;

        (int displacement, int bytesToCopy) SearchForNextToken(ReadOnlySpan<byte> buffer)
        {
            Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
            
            if (buffer.Length < MinDisplacement) return (0, 0);
            
            var searchOffset = _inputStream.Position;
            var biggestDisplacement = 0;
            var biggestToCopy = 0;
            
            for (var i = 0; i < buffer.Length - 1; i++)
            {
                _inputStream.Seek(searchOffset, SeekOrigin.Begin);

                var repeatCount = GetNextRepeatCount(buffer[i..]);
                
                if (repeatCount < MinBytesToCopy) continue;
                
                if (repeatCount > biggestToCopy)
                {
                    biggestToCopy = repeatCount;
                    biggestDisplacement = buffer.Length - i;
                }
            }

            _inputStream.Seek(searchOffset + biggestToCopy, SeekOrigin.Begin);

            return (biggestDisplacement, biggestToCopy);
        }
        
        int GetNextRepeatCount(ReadOnlySpan<byte> buffer)
        {
            Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");

            if (_inputStream.Position >= _inputStream.Length) return 0;
            
            var bufferIndex = 0;
                
            while (bufferIndex < MaxBytesToCopy && _inputStream.Position < _inputStream.Length)
            {
                if (buffer[bufferIndex % buffer.Length] != _reader.ReadByte()) break;
                bufferIndex++;
            }
            
            return bufferIndex;
        }

        _inputStream.Seek(0, SeekOrigin.Begin);
            
        _writer.Write((uint) (0x10 | _inputStream.Length << 8));
        
        var tempBuffer = ArrayPool<byte>.Shared.Rent(16);
        var tempBufferSize = 0;

        var flagData = 0;
        var flagIndex = 7;

        try
        {
            while (_inputStream.Position < _inputStream.Length)
            {
                var startIndex = _inputStream.Position - MaxDisplacement;
                startIndex = startIndex < 0 ? 0 : startIndex;
                var length = _inputStream.Position - startIndex;
            
                var nextToken = SearchForNextToken(_inputStream.GetBuffer().AsSpan((int) startIndex, (int) length));

                if (nextToken.displacement < MinDisplacement || nextToken.bytesToCopy < MinBytesToCopy)
                {
                    var dataToWrite = _reader.ReadByte();
                    tempBuffer[tempBufferSize++] = dataToWrite;
                }
                else
                {
                    var newDisplacement = nextToken.displacement - 1;
                    var newBytesToCopy = nextToken.bytesToCopy - 3;
                    tempBuffer[tempBufferSize++] = (byte) ((newDisplacement & 0xF00) >> 8 | newBytesToCopy << 4);
                    tempBuffer[tempBufferSize++] = (byte) (newDisplacement & 0xFF);

                    flagData |= 1 << flagIndex;
                }
            
                flagIndex--;

                if (flagIndex < 0)
                {
                    _writer.Write((byte) flagData);
                    _writer.Write(tempBuffer.AsSpan(0, tempBufferSize));

                    tempBufferSize = 0;
                    flagData = 0;
                    flagIndex = 7;
                }
            }

            if (flagIndex < 7)
            {
                _writer.Write((byte) flagData);
                _writer.Write(tempBuffer.AsSpan(0, tempBufferSize));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }
        
        _writer.Flush();
        
        _hasCompressed = true;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Mode == LzssStreamMode.Compress)
            {
                Flush();
            }
            
            _reader.Dispose();
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }
}
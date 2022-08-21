using System.Buffers;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace Fossil_Fighters_Tool.Archive.Compression.Rle;

public class RleStream : Stream
{
    public override bool CanRead => Mode == RleStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == RleStreamMode.Compress && BaseStream.CanWrite;

    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    [PublicAPI]
    public Stream BaseStream { get; }

    [PublicAPI]
    public RleStreamMode Mode { get; }
    
    private const int MaxRawDataLength = (1 << 7) - 1 + 1;
    private const int MinCompressDataLength = 3;
    private const int MaxCompressDataLength = (1 << 7) - 1 + 3;
    
    private MemoryStream? _inputStream;
    private MemoryStream? _outputStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    private bool _hasDecompressed;
    private bool _hasCompressed;

    public RleStream(Stream stream, RleStreamMode mode, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;
        
        if (mode == RleStreamMode.Decompress)
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
            if ((rawHeaderData & 0x30) != 0x30) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "RLE"));

            var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;
            _outputStream.Capacity = decompressSize;
            
            while (_outputStream.Length < decompressSize)
            {
                var flagRawData = _reader.ReadByte();
                var flagType = flagRawData >> 7;
                var flagData = flagRawData & 0x7F;

                if (flagType == 0)
                {
                    var repeatCount = flagData + 1;
                    
                    for (var i = repeatCount - 1; i >= 0; i--)
                    {
                        _writer.Write(_reader.ReadByte());
                    }
                }
                else
                {
                    var repeatCount = flagData + 3;
                    var repeatData = _reader.ReadByte();

                    for (var i = repeatCount - 1; i >= 0; i--)
                    {
                        _writer.Write(repeatData);
                    }
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

        int GetNextRepeatCount(Span<byte> buffer)
        {
            Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");

            if (_inputStream.Position >= _inputStream.Length) return 0;
                
            var bytesWritten = 0;
            var dataToCheck = _reader.ReadByte();
                
            buffer[bytesWritten++] = dataToCheck;
                
            while (bytesWritten < MaxCompressDataLength && _inputStream.Position < _inputStream.Length)
            {
                if (dataToCheck != _reader.ReadByte()) break;
                buffer[bytesWritten++] = dataToCheck;
            }

            _inputStream.Seek(-1, SeekOrigin.Current);

            return bytesWritten;
        }
            
        void WriteCompressed(byte data, int count)
        {
            _writer.Write((byte) (1 << 7 | (count - 3)));
            _writer.Write(data);
        }
            
        void WriteUncompressed(ReadOnlySpan<byte> buffer)
        {
            _writer.Write((byte) (buffer.Length - 1));
            _writer.Write(buffer);
        }
            
        _inputStream.Seek(0, SeekOrigin.Begin);
            
        _writer.Write((uint) (0x30 | _inputStream.Length << 8));

        var tempBuffer = ArrayPool<byte>.Shared.Rent(MaxCompressDataLength);
        var rawDataBuffer = ArrayPool<byte>.Shared.Rent(MaxRawDataLength);
            
        try
        {
            int tempBufferLength;
            var rawDataLength = 0;

            while ((tempBufferLength = GetNextRepeatCount(tempBuffer)) > 0)
            {
                if (tempBufferLength >= MinCompressDataLength)
                {
                    if (rawDataLength > 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;
                    }
                        
                    WriteCompressed(tempBuffer[0], tempBufferLength);
                }
                else
                {
                    var rawDataSpaceRemaining = MaxRawDataLength - rawDataLength;

                    if (rawDataSpaceRemaining == 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;
                            
                        tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                        rawDataLength += tempBufferLength;
                    }
                    else
                    {
                        if (tempBufferLength > rawDataSpaceRemaining)
                        {
                            tempBuffer.AsSpan(0, rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;
                            
                            WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                            rawDataLength = 0;
                            
                            tempBuffer.AsSpan(rawDataSpaceRemaining, tempBufferLength - rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += tempBufferLength - rawDataSpaceRemaining;
                        }
                        else
                        {
                            tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;
                        }
                    }
                }
            }
                
            if (rawDataLength > 0)
            {
                WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
            ArrayPool<byte>.Shared.Return(rawDataBuffer);
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
            if (Mode == RleStreamMode.Compress)
            {
                Flush();
            }
            
            _reader.Dispose();
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }
}
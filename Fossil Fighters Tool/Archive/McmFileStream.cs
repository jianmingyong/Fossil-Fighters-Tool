using System.Buffers;
using System.Diagnostics;
using System.Text;
using Fossil_Fighters_Tool.Archive.Compression.Huffman;
using Fossil_Fighters_Tool.Archive.Compression.Lzss;
using Fossil_Fighters_Tool.Archive.Compression.Rle;
using JetBrains.Annotations;

namespace Fossil_Fighters_Tool.Archive;

public class McmFileStream : Stream
{
    public override bool CanRead => Mode == McmFileStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == McmFileStreamMode.Compress && BaseStream.CanWrite;
    
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    [PublicAPI]
    public Stream BaseStream { get; }
    
    [PublicAPI]
    public McmFileStreamMode Mode { get; }
    
    [PublicAPI]
    public int DecompressFileSize { get; private set; }

    [PublicAPI]
    public int MaxSizePerChunk { get; set; } = 0x2000;
    
    [PublicAPI]
    public McmFileCompressionType CompressionType1 { get; set; }
    
    [PublicAPI]
    public McmFileCompressionType CompressionType2 { get; set; }

    private const int HeaderId = 0x004D434D;
    
    private MemoryStream? _inputStream;
    private MemoryStream? _outputStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    private bool _hasDecompressed;
    private bool _hasCompressed;

    public McmFileStream(Stream stream, McmFileStreamMode mode, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;
        
        if (mode == McmFileStreamMode.Decompress)
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
        
        Decompress();
        
        return _outputStream.Read(buffer, offset, count);
    }
    
    public override void WriteByte(byte value)
    {
        Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
        
        if (_hasCompressed) throw new IOException(Localization.StreamIsAlreadyCompressed);
        
        _inputStream.WriteByte(value);
        DecompressFileSize = (int) _inputStream.Length;
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");

        if (_hasCompressed) throw new IOException(Localization.StreamIsAlreadyCompressed);
        
        _inputStream.Write(buffer, offset, count);
        DecompressFileSize = (int) _inputStream.Length;
    }
    
    public override void Flush()
    {
        Compress();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    private void Compress()
    {
        if (_hasCompressed) return;
        
        _reader.BaseStream.Seek(0, SeekOrigin.Begin);
        
        _writer.Write(HeaderId);
        _writer.Write((int) _reader.BaseStream.Length);
        _writer.Write(MaxSizePerChunk);

        var numberOfChunks = (int) Math.Ceiling((double) _reader.BaseStream.Length / MaxSizePerChunk);
        _writer.Write(numberOfChunks);
        _writer.Write((byte) CompressionType1);
        _writer.Write((byte) CompressionType2);
        _writer.Write((short) 0);
        
        _writer.Write((int) _writer.BaseStream.Position + 4 * numberOfChunks + 4);

        var chunkOffset = _writer.BaseStream.Position;
        var dataOffset = chunkOffset + 4 * numberOfChunks;

        for (var i = 0; i < numberOfChunks; i++)
        {
            var chunkSize = Math.Min(_reader.BaseStream.Length - MaxSizePerChunk * i, MaxSizePerChunk);
            var buffer = ArrayPool<byte>.Shared.Rent((int) chunkSize);

            try
            {
                using var tempBuffer = new MemoryStream();

                Stream compressStream = CompressionType2 switch
                {
                    McmFileCompressionType.None => tempBuffer,
                    McmFileCompressionType.Rle => new RleStream(tempBuffer, RleStreamMode.Compress),
                    McmFileCompressionType.Lzss => new LzssStream(tempBuffer, LzssStreamMode.Compress),
                    McmFileCompressionType.Huffman => new HuffmanStream(tempBuffer, HuffmanStreamMode.Compress),
                    var _ => throw new ArgumentOutOfRangeException()
                };

                switch (CompressionType1)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressStream = new RleStream(compressStream, RleStreamMode.Compress);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressStream = new LzssStream(compressStream, LzssStreamMode.Compress);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressStream = new HuffmanStream(compressStream, HuffmanStreamMode.Compress);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                using var stream = new MemoryStream(buffer, 0, (int) chunkSize);
                if (_reader.BaseStream.Read(buffer, 0, (int) chunkSize) < chunkSize) throw new EndOfStreamException();

                stream.CopyTo(compressStream);
                compressStream.Flush();
                compressStream.Dispose();

                var dataLength = tempBuffer.Length;
                
                _writer.Seek((int) chunkOffset + 4 * i, SeekOrigin.Begin);
                _writer.Write((int) dataLength);
                
                _writer.Seek((int) dataOffset, SeekOrigin.Begin);
                tempBuffer.CopyTo(_writer.BaseStream);

                dataOffset += dataLength;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        
        _writer.Flush();

        _hasCompressed = true;
    }
    
    private void Decompress()
    {
        if (_hasDecompressed) return;
        
        var fileHeaderId = _reader.ReadInt32();
        if (fileHeaderId != HeaderId) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "MCM"));

        DecompressFileSize = _reader.ReadInt32();
        MaxSizePerChunk = _reader.ReadInt32();
        var numberOfChunk = _reader.ReadInt32();
            
        var dataChunkOffsets = new int[numberOfChunk + 1];
        CompressionType1 = (McmFileCompressionType) _reader.ReadByte();
        CompressionType2 = (McmFileCompressionType) _reader.ReadByte();

        _reader.ReadInt16();

        for (var i = 0; i < dataChunkOffsets.Length; i++)
        {
            dataChunkOffsets[i] = _reader.ReadInt32();
        }

        for (var i = 0; i < dataChunkOffsets.Length - 1; i++)
        {
            var requiredLength = dataChunkOffsets[i + 1] - dataChunkOffsets[i];
            var tempBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);

            try
            {
                Stream dataChunk = new MemoryStream(tempBuffer, 0, requiredLength);
                    
                BaseStream.Seek(dataChunkOffsets[i], SeekOrigin.Begin);
                if (BaseStream.Read(tempBuffer, 0, requiredLength) < requiredLength) throw new EndOfStreamException();

                var compressedStream = CompressionType1 switch
                {
                    McmFileCompressionType.None => dataChunk,
                    McmFileCompressionType.Rle => new RleStream(dataChunk, RleStreamMode.Decompress),
                    McmFileCompressionType.Lzss => new LzssStream(dataChunk, LzssStreamMode.Decompress),
                    McmFileCompressionType.Huffman => new HuffmanStream(dataChunk, HuffmanStreamMode.Decompress),
                    var _ => throw new ArgumentOutOfRangeException(null)
                };

                switch (CompressionType2)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressedStream = new RleStream(compressedStream, RleStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressedStream = new LzssStream(compressedStream, LzssStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressedStream = new HuffmanStream(compressedStream, HuffmanStreamMode.Decompress);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(null);
                }
                
                compressedStream.CopyTo(_writer.BaseStream);
                compressedStream.Dispose();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        Debug.Assert(_outputStream != null, nameof(_outputStream) + " != null");
        if (_outputStream.Length != DecompressFileSize) throw new InvalidDataException(Localization.StreamIsCorrupted);
            
        _writer.Seek(0, SeekOrigin.Begin);
        _hasDecompressed = true;
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Mode == McmFileStreamMode.Compress)
            {
                Flush();
            }
            
            _reader.Dispose();
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }
}
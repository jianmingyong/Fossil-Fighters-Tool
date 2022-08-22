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

        if (!_hasDecompressed)
        {
            var fileHeaderId = _reader.ReadUInt32();
            if (fileHeaderId != HeaderId) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "MCM"));

            var decompressFileSize = _reader.ReadUInt32();
            var maxSizePerChunk = _reader.ReadUInt32();
            var numberOfChunk = _reader.ReadUInt32();
            
            var dataChunkOffsets = new uint[numberOfChunk + 1];
            var compressionType1 = (McmFileCompressionType) _reader.ReadByte();
            var compressionType2 = (McmFileCompressionType) _reader.ReadByte();

            _reader.ReadInt16();

            for (var i = 0; i < dataChunkOffsets.Length; i++)
            {
                dataChunkOffsets[i] = _reader.ReadUInt32();
            }

            for (var i = 0; i < dataChunkOffsets.Length - 1; i++)
            {
                var requiredLength = dataChunkOffsets[i + 1] - dataChunkOffsets[i];

                Stream dataChunk = new ReadOnlyStream(BaseStream, dataChunkOffsets[i], requiredLength, true);

                var compressedStream = compressionType2 switch
                {
                    McmFileCompressionType.None => dataChunk,
                    McmFileCompressionType.Rle => new RleStream(dataChunk, RleStreamMode.Decompress, true),
                    McmFileCompressionType.Lzss => new LzssStream(dataChunk, LzssStreamMode.Decompress, true),
                    McmFileCompressionType.Huffman => new HuffmanStream(dataChunk, HuffmanStreamMode.Decompress, true),
                    var _ => throw new ArgumentOutOfRangeException()
                };

                switch (compressionType1)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressedStream = new RleStream(compressedStream, RleStreamMode.Decompress, true);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressedStream = new LzssStream(compressedStream, LzssStreamMode.Decompress, true);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressedStream = new HuffmanStream(compressedStream, HuffmanStreamMode.Decompress, true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                compressedStream.CopyTo(_outputStream);
                compressedStream.Dispose();
            }
            
            if (_outputStream.Length != decompressFileSize) throw new InvalidDataException(Localization.StreamIsCorrupted);
            
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
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
}
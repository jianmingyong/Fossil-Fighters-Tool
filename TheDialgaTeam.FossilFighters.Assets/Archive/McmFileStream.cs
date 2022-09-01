using System.Buffers;
using System.Diagnostics;
using JetBrains.Annotations;
using TheDialgaTeam.FossilFighters.Assets.Archive.Compression.Huffman;
using TheDialgaTeam.FossilFighters.Assets.Archive.Compression.Lzss;
using TheDialgaTeam.FossilFighters.Assets.Archive.Compression.Rle;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

public class McmFileStream : CompressibleStream
{
    [PublicAPI]
    public int DecompressFileSize { get; private set; }

    [PublicAPI]
    public int MaxSizePerChunk { get; set; } = 0x2000;

    [PublicAPI]
    public McmFileCompressionType CompressionType1
    {
        get => _compressionType1;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _compressionType1 = value;
        }
    }

    [PublicAPI]
    public McmFileCompressionType CompressionType2
    {
        get => _compressionType2;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _compressionType2 = value;
        }
    }
    
    private const int HeaderId = 0x004D434D;

    private McmFileCompressionType _compressionType1;
    private McmFileCompressionType _compressionType2;
    
    public McmFileStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream)
    {
        var fileHeaderId = reader.ReadInt32();
        if (fileHeaderId != HeaderId) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "MCM"));

        DecompressFileSize = reader.ReadInt32();
        MaxSizePerChunk = reader.ReadInt32();
        var numberOfChunk = reader.ReadInt32();
            
        var dataChunkOffsets = new int[numberOfChunk + 1];
        _compressionType1 = (McmFileCompressionType) reader.ReadByte();
        _compressionType2 = (McmFileCompressionType) reader.ReadByte();

        inputStream.Seek(2, SeekOrigin.Current);
        
        for (var i = 0; i < dataChunkOffsets.Length; i++)
        {
            dataChunkOffsets[i] = reader.ReadInt32();
        }
        
        for (var i = 0; i < dataChunkOffsets.Length - 1; i++)
        {
            var requiredLength = dataChunkOffsets[i + 1] - dataChunkOffsets[i];
            var tempBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);

            try
            {
                Stream dataChunk = new MemoryStream(tempBuffer, 0, requiredLength);
                    
                inputStream.Seek(dataChunkOffsets[i], SeekOrigin.Begin);
                if (inputStream.Read(tempBuffer, 0, requiredLength) < requiredLength) throw new EndOfStreamException();

                var compressedStream = CompressionType1 switch
                {
                    McmFileCompressionType.None => dataChunk,
                    McmFileCompressionType.Rle => new RleStream(dataChunk, CompressibleStreamMode.Decompress),
                    McmFileCompressionType.Lzss => new LzssStream(dataChunk, CompressibleStreamMode.Decompress),
                    McmFileCompressionType.Huffman => new HuffmanStream(dataChunk, CompressibleStreamMode.Decompress),
                    var _ => throw new ArgumentOutOfRangeException(null)
                };

                switch (CompressionType2)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressedStream = new RleStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressedStream = new LzssStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressedStream = new HuffmanStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(null);
                }
                
                compressedStream.CopyTo(outputStream);
                compressedStream.Dispose();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }
        
        if (outputStream.Length != DecompressFileSize) throw new InvalidDataException(Localization.StreamIsCorrupted);
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream)
    {
        throw new NotImplementedException();
    }
}
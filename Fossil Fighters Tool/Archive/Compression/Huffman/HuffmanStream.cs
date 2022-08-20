using System.Buffers;
using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanStream : Stream
{
    public override bool CanRead => Mode == HuffmanStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == HuffmanStreamMode.Compress && BaseStream.CanWrite;
    
    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    [PublicAPI]
    public Stream BaseStream { get; }
    
    [PublicAPI]
    public HuffmanStreamMode Mode { get; }
    
    [PublicAPI]
    public HuffmanDataSize DataSize
    {
        get => _dataSize;
        set
        {
            if (Mode == HuffmanStreamMode.Decompress) throw new NotSupportedException();
            _dataSize = value;
        }
    }
    
    private HuffmanDataSize _dataSize;
    
    private MemoryStream? _inputStream;
    private MemoryStream? _outputStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    private bool _hasDecompressed;
    private bool _hasCompressed;

    public HuffmanStream(Stream stream, HuffmanStreamMode mode, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;
        
        if (mode == HuffmanStreamMode.Decompress)
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
            if ((rawHeaderData & 0x20) != 0x20) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "Huffman"));

            _dataSize = (HuffmanDataSize) (rawHeaderData & 0xF);
            var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;
            _outputStream.Capacity = decompressSize;

            var treeSize = _reader.ReadByte();
            var treeNodeLength = (treeSize + 1) * 2 - 1;

            var rootNode = new HuffmanNode(_reader, _reader.BaseStream.Position, _reader.BaseStream.Position + treeNodeLength, _dataSize, false);
            var currentNode = rootNode;

            var isHalfDataWritten = false;
            var halfData = (byte) 0;

            while (_outputStream.Length < decompressSize)
            {
                var bitStream = _reader.ReadInt32();

                for (var index = 31; index >= 0; index--)
                {
                    var direction = (bitStream >> index) & 0x1;
                    
                    if (direction == 0)
                    {
                        currentNode = currentNode.Left ?? throw new InvalidDataException(Localization.HuffmanStreamInvalidBitstream);
                        if (!currentNode.Data.HasValue) continue;
                    }
                    else
                    {
                        currentNode = currentNode.Right ?? throw new InvalidDataException(Localization.HuffmanStreamInvalidBitstream);
                        if (!currentNode.Data.HasValue) continue;
                    }

                    if (_dataSize == HuffmanDataSize.FourBits)
                    {
                        if (isHalfDataWritten)
                        {
                            _writer.Write(halfData | currentNode.Data.Value << 4);
                            isHalfDataWritten = false;
                        }
                        else
                        {
                            halfData = currentNode.Data.Value;
                            isHalfDataWritten = true;
                        }
                    }
                    else
                    {
                        _writer.Write(currentNode.Data.Value);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush();
            
            _reader.Dispose();
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }
}
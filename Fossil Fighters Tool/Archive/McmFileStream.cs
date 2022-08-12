using System.Text;
using Fossil_Fighters_Tool.Archive.Compression.Huffman;

namespace Fossil_Fighters_Tool.Archive;

public class McmFileStream : Stream
{
    /*
        File Header
        0x00h 4     ID "MCM" (0x004D434D)
        0x04h 4     Decompressed data size
        0x08h 4     Max size per chunk in bytes (Usually 8kb)
        0x0Ch 4     Number of chunks
        0x10h 1     Compression Type 1 (0x00: None, 0x01: RLE, 0x02: LZSS, 0x03: Huffman)
        0x11h 1     Compression Type 2 (0x00: None, 0x01: RLE, 0x02: LZSS, 0x03: Huffman)
        0x12h 2     Padding
        0x14h N*4   Data Chunk offsets (Offset from MCM+0)
        ..    4     End of file (EOF) offset (Offset from MCM+0)
     */
    
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    private const int Id = 0x004D434D;

    private readonly MemoryStream _inputStream = new();
    private readonly BinaryReader _inputStreamReader;
    private long _inputStreamRead;
    
    private readonly Stream _outputStream;
    private readonly McmFileStreamMode _streamMode;
    private readonly bool _leaveOpen;
    
    // Decompress
    private bool _hasFirstHeaderChunk;
    private bool _hasSecondHeaderChunk;
    private int _decompressionSize;
    private int _maxSizePerChunk;
    private McmCompressionType _decompressionType1;
    private McmCompressionType _decompressionType2;
    private int[] _dataChunkOffsets;

    public McmFileStream(Stream outputStream, McmFileStreamMode streamMode, bool leaveOpen = false)
    {
        _inputStreamReader = new BinaryReader(_inputStream, Encoding.ASCII);
        _outputStream = outputStream;
        _streamMode = streamMode;
        _leaveOpen = leaveOpen;
        _dataChunkOffsets = Array.Empty<int>();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_streamMode == McmFileStreamMode.Decompress)
        {
            _inputStream.Write(buffer, offset, count);
            _inputStream.Seek(_inputStreamRead, SeekOrigin.Begin);
            Decompress();
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    private void Decompress()
    {
        if (!_hasFirstHeaderChunk)
        {
            if (_inputStream.Length - _inputStream.Position < 20) return;
            if (_inputStreamReader.ReadInt32() != Id) throw new InvalidDataException("The contents of the stream is not in the MCM format.");
            _inputStreamRead += 4;

            _decompressionSize = _inputStreamReader.ReadInt32();
            _inputStreamRead += 4;

            _maxSizePerChunk = _inputStreamReader.ReadInt32();
            _inputStreamRead += 4;

            _dataChunkOffsets = new int[_inputStreamReader.ReadInt32() + 1];
            _inputStreamRead += 4;

            _decompressionType1 = (McmCompressionType) _inputStreamReader.ReadByte();
            _decompressionType2 = (McmCompressionType) _inputStreamReader.ReadByte();
            _inputStreamReader.ReadInt16();
            _inputStreamRead += 4;

            _hasFirstHeaderChunk = true;
        }
        
        if (_hasFirstHeaderChunk && !_hasSecondHeaderChunk)
        {
            if (_inputStream.Length - _inputStream.Position < 4 * _dataChunkOffsets.Length) return;

            for (var i = 0; i < _dataChunkOffsets.Length; i++)
            {
                _dataChunkOffsets[i] = _inputStreamReader.ReadInt32();
                _inputStreamRead += 4;
            }
            
            _hasSecondHeaderChunk = true;
        }

        if (_hasFirstHeaderChunk && _hasSecondHeaderChunk)
        {
            using var tempOutputStream = new MemoryStream(_decompressionSize);
            
            for (var i = 0; i < _dataChunkOffsets.Length - 1; i++)
            {
                using var tempDecompressOutputStream = new MemoryStream(_maxSizePerChunk);
                
                // Try decompress the file in order
                switch (_decompressionType1)
                {
                    case McmCompressionType.None:
                    {
                        using var stream = new ReadOnlyStream(_inputStream, _dataChunkOffsets[i], _dataChunkOffsets[i + 1] - _dataChunkOffsets[i], true);
                        stream.CopyTo(tempDecompressOutputStream);
                        break;
                    }
                        
                    case McmCompressionType.Rle:
                        throw new NotImplementedException();
                    
                    case McmCompressionType.Lzss:
                        throw new NotImplementedException();
                    
                    case McmCompressionType.Huffman:
                    {
                        using var huffmanStream = new HuffmanStream(tempDecompressOutputStream, HuffmanStreamMode.Decompress, true);
                        using var stream = new ReadOnlyStream(_inputStream, _dataChunkOffsets[i], _dataChunkOffsets[i + 1] - _dataChunkOffsets[i], true);
                        stream.CopyTo(huffmanStream);
                        huffmanStream.Flush();
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                switch (_decompressionType2)
                {
                    case McmCompressionType.None:
                    {
                        tempDecompressOutputStream.Seek(0, SeekOrigin.Begin);
                        tempDecompressOutputStream.CopyTo(tempOutputStream);
                        break;
                    }
                    
                    case McmCompressionType.Rle:
                        throw new NotImplementedException();
                    
                    case McmCompressionType.Lzss:
                        throw new NotImplementedException();
                    
                    case McmCompressionType.Huffman:
                    {
                        using var huffmanStream = new HuffmanStream(tempOutputStream, HuffmanStreamMode.Decompress, true);
                        tempDecompressOutputStream.Seek(0, SeekOrigin.Begin);
                        tempDecompressOutputStream.CopyTo(huffmanStream);
                        huffmanStream.Flush();
                        break;
                    }

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            tempOutputStream.Seek(0, SeekOrigin.Begin);
            tempOutputStream.CopyTo(_outputStream);
        }
    }

    private void Compress()
    {
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        _outputStream.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_leaveOpen)
            {
                _outputStream.Dispose();
            }
            
            _inputStream.Dispose();
            _inputStreamReader.Dispose();
        }
        
        base.Dispose(disposing);
    }
}
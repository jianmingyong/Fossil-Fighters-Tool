using System.Text;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanStream : Stream
{
    /*
     * Data Header
     * Bit 0-3  Data size in bit units (normally 4 or 8)
     * Bit 4-7  Compressed type (must be 2 for Huffman)
     * Bit 8-31 24bit size of decompressed data in bytes
     *
     * Tree Size
     * Bit 0-7  Size of Tree Table/2-1
     *
     * Tree Table (list of 8bit nodes, starting with the root node)
     * Root Node and Non-Data-Child Nodes are:
     * Bit 0-5  Offset to next child node,
     *          Next child node0 is at (CurrentAddr AND NOT 1)+Offset*2+2
     *          Next child node1 is at (CurrentAddr AND NOT 1)+Offset*2+2+1
     * Bit 6    Node1 End Flag (1=Next child node is data)
     * Bit 7    Node0 End Flag (1=Next child node is data)
     * Data nodes are (when End Flag was set in parent node):
     * Bit 0-7  Data (upper bits should be zero if Data Size is less than 8)
     *
     * Compressed Bitstream (stored in units of 32bits)
     * Bit 0-31 Node Bits (Bit31=First Bit)  (0=Node0, 1=Node1)
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

    public HuffmanDataSize DataSize
    {
        get => _dataSize;
        set
        {
            if (_streamMode == HuffmanStreamMode.Decompress) throw new NotSupportedException();
            _dataSize = value;
        }
    }

    private readonly MemoryBinaryStream _inputStream;
    private long _inputStreamRead;
    private long _inputStreamWritten;
    
    private readonly Stream _outputStream;
    private readonly HuffmanStreamMode _streamMode;
    private readonly bool _leaveOpen;
    
    // Shared between decompress and compress
    private HuffmanDataSize _dataSize;
    
    // Decompress
    private bool _hasDataHeader;
    private bool _hasTreeSize;
    private bool _hasTreeBuilt;
    private int _decompressLength;
    private int _treeSize;
    private int _treeNodeLength;
    private HuffmanNode? _rootNode;
    private HuffmanNode? _currentNode;
    private bool _isHalfDataWritten;
    private byte _halfData;
    private int _writtenLength;

    public HuffmanStream(Stream outputStream, HuffmanStreamMode streamMode, bool leaveOpen = false)
    {
        _inputStream = new MemoryBinaryStream(Encoding.ASCII);
        _outputStream = outputStream;
        _streamMode = streamMode;
        _leaveOpen = leaveOpen;
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inputStream.Seek(_inputStreamWritten, SeekOrigin.Begin);
        _inputStream.Write(buffer, offset, count);
        _inputStream.Seek(_inputStreamRead, SeekOrigin.Begin);
        
        _inputStreamWritten += count;
        
        if (_streamMode == HuffmanStreamMode.Decompress)
        {
            Decompress();
        }
        else
        {
            Compress();
        }
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

    private void Decompress()
    {
        if (!_hasDataHeader)
        {
            if (_inputStream.Length - _inputStream.Position < 4) return;
            
            var rawDataHeader = _inputStream.ReadUInt32();
                
            if (((rawDataHeader >> 4) & 0xF) != 0x2)
            {
                throw new InvalidDataException("The contents of the stream is not in the huffman format.");
            }
                
            _dataSize = (HuffmanDataSize) (rawDataHeader & 0xF);
            _decompressLength = (int) (rawDataHeader >> 8);
            _inputStreamRead += 4;
            
            _hasDataHeader = true;
        }

        if (_hasDataHeader && !_hasTreeSize)
        {
            if (_inputStream.Length - _inputStream.Position < 1) return;

            _treeSize = _inputStream.ReadByte();
            _treeNodeLength = (_treeSize + 1) * 2 - 1;
            _inputStreamRead += 1;
            
            _hasTreeSize = true;
        }

        if (_hasDataHeader && _hasTreeSize && !_hasTreeBuilt)
        {
            if (_inputStream.Length - _inputStream.Position < _treeNodeLength) return;
            
            var startOffset = _inputStream.Position;
            
            _rootNode = new HuffmanNode(_inputStream, _inputStream.Position, _dataSize, false);
            _currentNode = _rootNode;
            
            _inputStream.Seek(startOffset + _treeNodeLength, SeekOrigin.Begin);
            _inputStreamRead = _inputStream.Position;
            
            _hasTreeBuilt = true;
        }

        if (_hasDataHeader && _hasTreeSize && _hasTreeBuilt)
        {
            // Compressed Bitstream
            while (_inputStream.Length - _inputStream.Position >= 4)
            {
                var bitStream = _inputStream.ReadUInt32();
                _inputStreamRead += 4;
                
                for (var index = 31; index >= 0; index--)
                {
                    var direction = (bitStream >> index) & 0x01;

                    if (direction == 0)
                    {
                        _currentNode = _currentNode!.Left ?? throw new InvalidDataException("The contents of the stream contains invalid bitstream.");
                        if (!_currentNode.Data.HasValue) continue;
                    }
                    else
                    {
                        _currentNode = _currentNode!.Right ?? throw new InvalidDataException("The contents of the stream contains invalid bitstream.");
                        if (!_currentNode.Data.HasValue) continue;
                    }
                    
                    if (_dataSize == HuffmanDataSize.FourBits)
                    {
                        if (_isHalfDataWritten)
                        {
                            _outputStream.WriteByte((byte) (_halfData | (_currentNode.Data.Value << 4)));
                            _isHalfDataWritten = false;
                            _writtenLength += 1;
                        }
                        else
                        {
                            _halfData = _currentNode.Data.Value;
                            _isHalfDataWritten = true;
                        }
                            
                        _currentNode = _rootNode;
                    }
                    else
                    {
                        _outputStream.WriteByte(_currentNode.Data.Value);
                        _writtenLength += 1;
                        _currentNode = _rootNode;
                    }
                    
                    if (_writtenLength == _decompressLength) break;
                }
                
                if (_writtenLength == _decompressLength) break;
            }
            
            _outputStream.Flush();
        }
    }
    
    private void Compress()
    {
        throw new NotImplementedException();
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
        }
        
        base.Dispose(disposing);
    }
}
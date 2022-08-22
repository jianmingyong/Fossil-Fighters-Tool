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

            var rootPosition = _reader.BaseStream.Position;

            var rootNode = new HuffmanNode(_reader, rootPosition, rootPosition + treeNodeLength, _dataSize, false);
            var currentNode = rootNode;

            var isHalfDataWritten = false;
            var halfData = (byte) 0;

            _reader.BaseStream.Seek(rootPosition + treeNodeLength, SeekOrigin.Begin);

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
                            _writer.Write((byte) (halfData | currentNode.Data.Value << 4));
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

                    currentNode = rootNode;
                    
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

        Dictionary<byte, int> BuildHuffmanFourBitsTable()
        {
            Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
            
            var result = new Dictionary<byte, int>();
            
            _inputStream.Seek(0, SeekOrigin.Begin);

            while (_inputStream.Position < _inputStream.Length)
            {
                var value = _reader.ReadByte();
                var highBit = (byte) ((value >> 4) & 0xF);
                var lowBit = (byte) (value & 0xF);

                if (result.ContainsKey(highBit))
                {
                    result[highBit]++;
                }
                else
                {
                    result.Add(highBit, 1);
                }
                
                if (result.ContainsKey(lowBit))
                {
                    result[lowBit]++;
                }
                else
                {
                    result.Add(lowBit, 1);
                }
            }
            
            return result;
        }
        
        Dictionary<byte, int> BuildHuffmanEightBitsTable()
        {
            Debug.Assert(_inputStream != null, nameof(_inputStream) + " != null");
            
            var result = new Dictionary<byte, int>();
            
            _inputStream.Seek(0, SeekOrigin.Begin);

            while (_inputStream.Position < _inputStream.Length)
            {
                var value = _reader.ReadByte();
                
                if (result.ContainsKey(value))
                {
                    result[value]++;
                }
                else
                {
                    result.Add(value, 1);
                }
            }
            
            return result;
        }
        
        int NodesCount(HuffmanNode node)
        {
            if (node.Left != null && node.Right != null) return 1 + NodesCount(node.Left) + NodesCount(node.Right);
            if (node.Left != null) return 1 + NodesCount(node.Left);
            if (node.Right != null) return 1 + NodesCount(node.Right);
            return 1;
        }

        void UpdateHuffmanNodes(HuffmanNode node)
        {
            var position = 5;
       
            for (var bits = 0; bits < 32; bits++)
            {
                if (bits == 0)
                {
                    node.Position = position++;
                    continue;
                }
                
                var maxValue = Math.Pow(2, bits);
                var hasNodes = false;

                for (var value = 0; value < maxValue; value++)
                {
                    var currentNode = node;

                    for (var rightShift = bits - 1; rightShift >= 0; rightShift--)
                    {
                        var direction = (value >> rightShift) & 0x1;
                        currentNode = direction == 0 ? currentNode?.Left : currentNode?.Right;
                    }

                    if (currentNode != null)
                    {
                        if (currentNode.Data.HasValue)
                        {
                            currentNode.BitstreamValue = value;
                            currentNode.BitstreamLength = bits;
                        }
                        
                        currentNode.Position = position++;
                        hasNodes = true;
                    }
                }
                
                if (!hasNodes) break;
            }
        }

        void WriteHuffmanNodes(HuffmanNode node)
        {
            if (node.Data.HasValue)
            {
                _writer.BaseStream.Seek(node.Position, SeekOrigin.Begin);
                _writer.Write(node.Data.Value);
            }
            else
            {
                var offset = (node.Left!.Position - (node.Position & ~1L) - 2) / 2;
                var flag = 0;

                if (node.Left?.Data.HasValue ?? false)
                {
                    flag |= 1 << 1;
                }
                
                if (node.Right?.Data.HasValue ?? false)
                {
                    flag |= 1;
                }
                
                _writer.BaseStream.Seek(node.Position, SeekOrigin.Begin);
                _writer.Write((byte) (offset | (uint) (flag << 6)));

                if (node.Left != null)
                {
                    WriteHuffmanNodes(node.Left);
                }
                
                if (node.Right != null)
                {
                    WriteHuffmanNodes(node.Right);
                }
            }
        }

        void WriteHuffmanBitstream(IReadOnlyDictionary<byte, HuffmanNode> nodes)
        {
            _reader.BaseStream.Seek(0, SeekOrigin.Begin);

            var bitStream = 0u;
            var bitsLeft = 32;
            
            while (_reader.BaseStream.Position < _reader.BaseStream.Length)
            {
                var value = _reader.ReadByte();

                if (_dataSize == HuffmanDataSize.FourBits)
                {
                    var firstValue = nodes[(byte) (value & 0xF)];
                    var secondValue = nodes[(byte) ((value >> 4) & 0xF)];

                    for (var i = firstValue.BitstreamLength - 1; i >= 0; i--)
                    {
                        var bitValue = (byte) ((firstValue.BitstreamValue >> i) & 0x1);
                        
                        if (bitsLeft > 0)
                        {
                            bitStream <<= 1;
                            bitStream |= bitValue;
                            bitsLeft--;
                        }
                        
                        if (bitsLeft == 0)
                        {
                            _writer.Write(bitStream);
                            bitsLeft = 32;
                        }
                    }
                    
                    for (var i = secondValue.BitstreamLength - 1; i >= 0; i--)
                    {
                        var bitValue = (byte) ((secondValue.BitstreamValue >> i) & 0x1);
                        
                        if (bitsLeft > 0)
                        {
                            bitStream <<= 1;
                            bitStream |= bitValue;
                            bitsLeft--;
                        }
                        
                        if (bitsLeft == 0)
                        {
                            _writer.Write(bitStream);
                            bitsLeft = 32;
                        }
                    }
                }
                else
                {
                    var firstValue = nodes[value];

                    for (var i = firstValue.BitstreamLength - 1; i >= 0; i--)
                    {
                        var bitValue = (byte) ((firstValue.BitstreamValue >> i) & 0x1);
                        
                        if (bitsLeft > 0)
                        {
                            bitStream <<= 1;
                            bitStream |= bitValue;
                            bitsLeft--;
                        }
                        
                        if (bitsLeft == 0)
                        {
                            _writer.Write(bitStream);
                            bitsLeft = 32;
                        }
                    }
                }
            }

            if (bitsLeft > 0)
            {
                bitStream <<= bitsLeft;
                _writer.Write(bitStream);
            }
        }

        Dictionary<byte, int> huffmanFrequencyTable;

        switch (_dataSize)
        {
            case HuffmanDataSize.Auto:
            {
                var fourBits = BuildHuffmanFourBitsTable();
                var eightBits = BuildHuffmanEightBitsTable();

                if (fourBits.Count < eightBits.Count)
                {
                    huffmanFrequencyTable = fourBits;
                    _dataSize = HuffmanDataSize.FourBits;
                }
                else
                {
                    huffmanFrequencyTable = eightBits;
                    _dataSize = HuffmanDataSize.EightBits;
                }
                break;
            }

            case HuffmanDataSize.FourBits:
                huffmanFrequencyTable = BuildHuffmanFourBitsTable();
                break;
            
            case HuffmanDataSize.EightBits:
                huffmanFrequencyTable = BuildHuffmanEightBitsTable();
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        if (huffmanFrequencyTable.Count < 2) throw new InvalidDataException(Localization.HuffmanStreamDatasetTooSmall);

        var nodes = new PriorityQueue<HuffmanNode, int>();
        var dataNodes = new Dictionary<byte, HuffmanNode>();

        foreach (var table in huffmanFrequencyTable)
        {
            var node = new HuffmanNode { Data = table.Key, Value = table.Value };
            nodes.Enqueue(node, table.Value);
            dataNodes.Add(table.Key, node);
        }

        while (nodes.Count > 1)
        {
            var nodeA = nodes.Dequeue();
            var nodeB = nodes.Dequeue();

            var nodeC = new HuffmanNode { Left = nodeA, Right = nodeB, Value = nodeA.Value + nodeB.Value };
            nodeA.Parent = nodeC;
            nodeB.Parent = nodeC;
            
            nodes.Enqueue(nodeC, nodeC.Value);
        }
        
        var rootNode = nodes.Dequeue();
        var nodesCount = NodesCount(rootNode);
        
        UpdateHuffmanNodes(rootNode);
        
        _writer.Write((uint) _dataSize | (uint) 0x2 << 4 | (uint) _inputStream.Length << 8);
        _writer.Write((byte) ((nodesCount - 1) / 2));
        
        WriteHuffmanNodes(rootNode);
        WriteHuffmanBitstream(dataNodes);

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
            if (Mode == HuffmanStreamMode.Compress)
            {
                Flush();
            }

            _reader.Dispose();
            _writer.Dispose();
        }

        base.Dispose(disposing);
    }
}
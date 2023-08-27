// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2022 Yong Jian Ming
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace TheDialgaTeam.FossilFighters.Assets.Archive.Compression;

public sealed class HuffmanStream : CompressibleStream
{
    public HuffmanDataSize DataSize
    {
        get => _dataSize;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _dataSize = value;
        }
    }

    private const int CompressionHeader = 2 << 4;
    private const int MaxInputDataLength = (1 << 24) - 1;

    private HuffmanDataSize _dataSize;

    public HuffmanStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream)
    {
        var rawHeaderData = reader.ReadInt32();
        if ((rawHeaderData & 0xF0) != CompressionHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "Huffman"));

        _dataSize = (HuffmanDataSize) (rawHeaderData & 0xF);
        var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;

        if (outputStream.Capacity < decompressSize)
        {
            outputStream.Capacity = decompressSize;
        }

        var treeSize = reader.ReadByte();
        var treeNodeLength = (treeSize + 1) * 2 - 1;

        var rootPosition = reader.BaseStream.Position;

        var rootNode = new HuffmanNode(reader, rootPosition, rootPosition + treeNodeLength, _dataSize, false);
        var currentNode = rootNode;

        var isHalfDataWritten = false;
        var halfData = (byte) 0;

        inputStream.Seek(rootPosition + treeNodeLength, SeekOrigin.Begin);

        while (outputStream.Length < decompressSize)
        {
            var bitStream = reader.ReadInt32();

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
                        writer.Write((byte) (halfData | (currentNode.Data.Value << 4)));
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
                    writer.Write(currentNode.Data.Value);
                }

                currentNode = rootNode;

                if (outputStream.Length >= decompressSize) break;
            }
        }
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream)
    {
        var dataLength = inputStream.Length;
        if (dataLength > MaxInputDataLength) throw new InvalidDataException(string.Format(Localization.StreamDataTooLarge, "Huffman"));

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

            nodes.Enqueue(nodeC, nodeC.Value);
        }

        var rootNode = nodes.Dequeue();
        var nodesCount = NodesCount(rootNode);

        UpdateHuffmanNodes(rootNode);

        writer.Write((uint) _dataSize | CompressionHeader | ((uint) inputStream.Length << 8));

        var treeSize = (nodesCount - 1) / 2;
        writer.Write((byte) treeSize);

        var rootPosition = outputStream.Position;

        WriteHuffmanNodes(rootNode);

        var treeNodeLength = (treeSize + 1) * 2 - 1;
        writer.Seek((int) (rootPosition + treeNodeLength), SeekOrigin.Begin);

        WriteHuffmanBitstream(dataNodes);

        while (writer.BaseStream.Length % 4 != 0)
        {
            writer.Write((byte) 0);
        }

        return;

        Dictionary<byte, int> BuildHuffmanFourBitsTable()
        {
            var result = new Dictionary<byte, int>();

            inputStream.Seek(0, SeekOrigin.Begin);

            while (inputStream.Position < inputStream.Length)
            {
                var value = reader.ReadByte();
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
            var result = new Dictionary<byte, int>();

            inputStream.Seek(0, SeekOrigin.Begin);

            while (inputStream.Position < inputStream.Length)
            {
                var value = reader.ReadByte();

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
            if (node is { Left: not null, Right: not null }) return 1 + NodesCount(node.Left) + NodesCount(node.Right);
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

                    if (currentNode == null) continue;

                    if (currentNode.Data.HasValue)
                    {
                        currentNode.BitstreamValue = value;
                        currentNode.BitstreamLength = bits;
                    }

                    currentNode.Position = position++;
                    hasNodes = true;
                }

                if (!hasNodes) break;
            }
        }

        void WriteHuffmanNodes(HuffmanNode node)
        {
            if (node.Data.HasValue)
            {
                writer.Seek((int) node.Position, SeekOrigin.Begin);
                writer.Write(node.Data.Value);
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

                writer.Seek((int) node.Position, SeekOrigin.Begin);
                writer.Write((byte) (offset | (uint) (flag << 6)));

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
            inputStream.Seek(0, SeekOrigin.Begin);

            var bitStream = 0u;
            var bitsLeft = 32;

            while (inputStream.Position < inputStream.Length)
            {
                var value = reader.ReadByte();

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
                            writer.Write(bitStream);
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
                            writer.Write(bitStream);
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
                            writer.Write(bitStream);
                            bitsLeft = 32;
                        }
                    }
                }
            }

            if (bitsLeft < 32)
            {
                bitStream <<= bitsLeft;
                writer.Write(bitStream);
            }
        }
    }
}
using System.Diagnostics;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanNode
{
    //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public HuffmanNode? Parent { get; set; }
    
    public HuffmanNode? Left { get; set; }
    
    public HuffmanNode? Right { get; set; }
    
    public byte? Data { get; set; }
    
    //[DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public int? Value { get; set; }
    
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public long Position { get; set; } = 5;

    public HuffmanNode()
    {
    }
    
    public HuffmanNode(BinaryReader reader, long position, long endPosition, HuffmanDataSize dataSize, bool isData)
    {
        reader.BaseStream.Seek(position, SeekOrigin.Begin);

        var rawByte = reader.ReadByte();
        
        if (isData)
        {
            if (dataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new InvalidDataException(Localization.HuffmanStreamInvalidDataNode);
            Data = rawByte;
        }
        else
        {
            var offset = rawByte & 0x3F;

            var leftOffset = (position & ~1L) + offset * 2 + 2;
            var rightOffset = (position & ~1L) + offset * 2 + 2 + 1;

            if (leftOffset < endPosition)
            {
                Left = new HuffmanNode(reader, leftOffset, endPosition, dataSize, (rawByte & 0x80) > 0);
            }

            if (rightOffset < endPosition)
            {
                Right = new HuffmanNode(reader, rightOffset, endPosition, dataSize, (rawByte & 0x40) > 0);
            }
        }
    }
}
using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanNode
{
    public HuffmanNode? Left { get; }
    
    public HuffmanNode? Right { get; }
    
    public byte? Data { get; }

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

    [Obsolete("To be removed in v1.3.")]
    public HuffmanNode(SequenceReader<byte> reader, long position, HuffmanDataSize dataSize, bool isData)
    {
        reader.Advance(position - reader.Consumed);
        reader.TryRead(out var rawByte);
        
        if (isData)
        {
            if (dataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new InvalidDataException(Localization.HuffmanStreamInvalidDataNode);
            Data = rawByte;
        }
        else
        {
            var offset = rawByte & 0x3F;
            
            Left = new HuffmanNode(reader, (position & ~1L) + offset * 2 + 2, dataSize, (rawByte & 0x80) > 0);
            Right = new HuffmanNode(reader, (position & ~1L) + offset * 2 + 2 + 1, dataSize, (rawByte & 0x40) > 0);
        }
    }
}
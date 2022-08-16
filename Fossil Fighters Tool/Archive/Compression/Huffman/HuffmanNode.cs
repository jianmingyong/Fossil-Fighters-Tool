using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanNode
{
    public HuffmanNode? Left { get; }
    
    public HuffmanNode? Right { get; }
    
    public byte? Data { get; }

    public HuffmanNode(SequenceReader<byte> reader, long position, HuffmanDataSize dataSize, bool isData)
    {
        reader.Advance(position - reader.Consumed);
        reader.TryRead(out var rawByte);
        
        if (isData)
        {
            if (dataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new InvalidDataException("The contents of the stream contains invalid data node.");
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
using System.Text;

namespace Fossil_Fighters_Tool.Archive.Compression.Huffman;

public class HuffmanNode
{
    public HuffmanNode? Left { get; }
    
    public HuffmanNode? Right { get; }
    
    public byte? Data { get; }

    public HuffmanNode(Stream stream, long position, HuffmanDataSize dataSize, bool isData)
    {
        /*
         * Tree Table (list of 8bit nodes, starting with the root node)
         * Root Node and Non-Data-Child Nodes are:
         * Bit 0-5  Offset to next child node,
         *          Next child node0 is at (CurrentAddr AND NOT 1)+Offset*2+2
         *          Next child node1 is at (CurrentAddr AND NOT 1)+Offset*2+2+1
         * Bit 6    Node1 End Flag (1=Next child node is data)
         * Bit 7    Node0 End Flag (1=Next child node is data)
         * Data nodes are (when End Flag was set in parent node):
         * Bit 0-7  Data (upper bits should be zero if Data Size is less than 8)
         */

        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        stream.Seek(position, SeekOrigin.Begin);
        
        var rawByte = reader.ReadByte();
        
        if (isData)
        {
            if (dataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new InvalidDataException("The contents of the stream contains invalid data node.");
            Data = rawByte;
        }
        else
        {
            var offset = rawByte & 0x3F;
            Left = new HuffmanNode(stream, (position & ~1L) + offset * 2 + 2, dataSize, (rawByte & 0x80) > 0);
            Right = new HuffmanNode(stream, (position & ~1L) + offset * 2 + 2 + 1, dataSize, (rawByte & 0x40) > 0);
        }
    }
}
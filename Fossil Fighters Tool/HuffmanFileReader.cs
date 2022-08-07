using System.Text;

namespace Fossil_Fighters_Tool;

public class HuffmanFileReader : IDisposable
{
    public class HuffmanNode
    {
        public HuffmanNode? Left { get; }
        
        public HuffmanNode? Right { get; }
        
        public byte? Data { get; }

        public HuffmanNode(HuffmanFileReader reader, long position, bool isData)
        {
            reader._stream.Seek(position, SeekOrigin.Begin);
            
            using var binaryReader = new BinaryReader(reader._stream, Encoding.ASCII, true);
            var rawByte = binaryReader.ReadByte();
            
            if (isData)
            {
                if (reader.DataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new Exception("Invalid Data node.");
                Data = rawByte;
            }
            else
            {
                var offset = rawByte & 0x3F;
                Left = new HuffmanNode(reader, (position & ~1L) + offset * 2 + 2, (rawByte & 0x80) > 0);
                Right = new HuffmanNode(reader, (position & ~1L) + offset * 2 + 2 + 1, (rawByte & 0x40) > 0);
            }
        }
    }
    
    public enum HuffmanDataSize
    {
        FourBits = 4,
        EightBits = 8
    }
    
    public HuffmanDataSize DataSize { get; }
    
    public int DecompressSize { get; }
    
    public byte TreeSize { get; }
    
    public HuffmanNode RootNode { get; }
    
    public int[] CompressedBitstream { get; }
    
    private readonly FileStream _stream;

    public HuffmanFileReader(FileStream stream)
    {
        _stream = stream;
        
        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);

        var header = binaryReader.ReadByte();
        
        if (((header >> 4) & 0x0F) != 2)
        {
            throw new Exception("This is not a Huffman compressed file.");
        }
        
        if ((header & 0x0F) != 4 && (header & 0x0F) != 8)
        {
            throw new Exception("This is not a Huffman compressed file.");
        }
        
        DataSize = (HuffmanDataSize) (header & 0x0F);

        var secondHeader = binaryReader.ReadInt32();

        DecompressSize = secondHeader & 0xFFFFFF;
        TreeSize = (byte) (secondHeader >> 24);
        RootNode = new HuffmanNode(this, stream.Position, false);

        var compressedBitstream = new List<int>();

        while (_stream.Position < _stream.Length)
        {
            if (_stream.Position + 4 <= _stream.Length)
            {
                compressedBitstream.Add(binaryReader.ReadInt32());
            }
            else if (_stream.Position + 3 <= _stream.Length)
            {
                compressedBitstream.Add(binaryReader.ReadByte() + binaryReader.ReadInt16() << 8);
            }
            else if (_stream.Position + 2 <= _stream.Length)
            {
                compressedBitstream.Add(binaryReader.ReadInt16());
            }
            else
            {
                compressedBitstream.Add(binaryReader.ReadByte());
            }
        }
        
        CompressedBitstream = compressedBitstream.ToArray();
    }

    public void Decompress(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }
        
        using var fileStream = new FileStream(Path.Combine(outputDirectory, $"{Path.GetFileName(_stream.Name)}.decompressed"), FileMode.Create);

        var dataWritten = 0;
        byte dataToWrite = 0;
        var isHalfDataWritten = false;
        var currentNode = RootNode;

        foreach (var bitStream in CompressedBitstream)
        {
            for (var index = 31; index >= 0; index--)
            {
                var direction = (bitStream >> index) & 0x01;

                if (direction == 0)
                {
                    currentNode = currentNode.Left ?? throw new Exception("Invalid bitstream.");
                    if (!currentNode.Data.HasValue) continue;

                    if (DataSize == HuffmanDataSize.FourBits)
                    {
                        if (isHalfDataWritten)
                        {
                            fileStream.WriteByte((byte) (dataToWrite | (currentNode.Data.Value << 4)));
                            fileStream.Flush();
                            isHalfDataWritten = false;
                            currentNode = RootNode;
                            dataWritten++;
                        }
                        else
                        {
                            dataToWrite = currentNode.Data.Value;
                            isHalfDataWritten = true;
                            currentNode = RootNode;
                        }
                    }
                    else
                    {
                        fileStream.WriteByte(currentNode.Data.Value);
                        fileStream.Flush();
                        currentNode = RootNode;
                        dataWritten++;
                    }
                }
                else
                {
                    currentNode = currentNode.Right ?? throw new Exception("Invalid bitstream.");
                    if (!currentNode.Data.HasValue) continue;

                    if (DataSize == HuffmanDataSize.FourBits)
                    {
                        if (isHalfDataWritten)
                        {
                            fileStream.WriteByte((byte) (dataToWrite | (currentNode.Data.Value << 4)));
                            fileStream.Flush();
                            isHalfDataWritten = false;
                            currentNode = RootNode;
                            dataWritten++;
                        }
                        else
                        {
                            dataToWrite = currentNode.Data.Value;
                            isHalfDataWritten = true;
                            currentNode = RootNode;
                        }
                    }
                    else
                    {
                        fileStream.WriteByte(currentNode.Data.Value);
                        fileStream.Flush();
                        currentNode = RootNode;
                        dataWritten++;
                    }
                }

                if (dataWritten == DecompressSize) break;
            }
            
            if (dataWritten == DecompressSize) break;
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
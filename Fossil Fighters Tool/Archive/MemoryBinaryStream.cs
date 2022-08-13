using System.Text;

namespace Fossil_Fighters_Tool.Archive;

public class MemoryBinaryStream : Stream
{
    public override bool CanRead => _memoryStream.CanRead;
    public override bool CanSeek => _memoryStream.CanSeek;
    public override bool CanWrite => _memoryStream.CanWrite;
    
    public override long Length => _memoryStream.Length;

    public override long Position
    {
        get => _memoryStream.Position;
        set => _memoryStream.Position = value;
    }

    private readonly MemoryStream _memoryStream = new();
    private readonly BinaryReader _binaryReader;
    private readonly BinaryWriter _binaryWriter;

    public MemoryBinaryStream() : this(Encoding.UTF8)
    {
    }
    
    public MemoryBinaryStream(Encoding encoding)
    {
        _binaryReader = new BinaryReader(_memoryStream, encoding);
        _binaryWriter = new BinaryWriter(_memoryStream, encoding);
    }
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        return _memoryStream.Read(buffer, offset, count);
    }

    public int Read(char[] buffer, int offset, int count)
    {
        return _binaryReader.Read(buffer, offset, count);
    }

    public int Read(Span<char> buffer)
    {
        return _binaryReader.Read(buffer);
    }

    public int PeekChar()
    {
        return _binaryReader.PeekChar();
    }

    public char ReadChar()
    {
        return _binaryReader.ReadChar();
    }
    
    public char[] ReadChars(int count)
    {
        return _binaryReader.ReadChars(count);
    }
    
    public string ReadString()
    {
        return _binaryReader.ReadString();
    }

    public bool ReadBoolean()
    {
        return _binaryReader.ReadBoolean();
    }
    
    public new byte ReadByte()
    {
        return _binaryReader.ReadByte();
    }

    public byte[] ReadBytes(int count)
    {
        return _binaryReader.ReadBytes(count);
    }
    
    public sbyte ReadSByte()
    {
        return _binaryReader.ReadSByte();
    }

    public short ReadInt16()
    {
        return _binaryReader.ReadInt16();
    }
    
    public ushort ReadUInt16()
    {
        return _binaryReader.ReadUInt16();
    }

    public int ReadInt32()
    {
        return _binaryReader.ReadInt32();
    }
    
    public uint ReadUInt32()
    {
        return _binaryReader.ReadUInt32();
    }
    
    public long ReadInt64()
    {
        return _binaryReader.ReadInt64();
    }

    public ulong ReadUInt64()
    {
        return _binaryReader.ReadUInt64();
    }
    
    public int Read7BitEncodedInt()
    {
        return _binaryReader.Read7BitEncodedInt();
    }

    public long Read7BitEncodedInt64()
    {
        return _binaryReader.Read7BitEncodedInt64();
    }

    public Half ReadHalf()
    {
        return _binaryReader.ReadHalf();
    }
    
    public float ReadSingle()
    {
        return _binaryReader.ReadSingle();
    }

    public double ReadDouble()
    {
        return _binaryReader.ReadDouble();
    }
    
    public decimal ReadDecimal()
    {
        return _binaryReader.ReadDecimal();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _memoryStream.Write(buffer, offset, count);
    }

    public void Write(char[] chars, int offset, int count)
    {
        _binaryWriter.Write(chars, offset, count);
    }
    
    public void Write(ReadOnlySpan<char> chars)
    {
        _binaryWriter.Write(chars);
    }

    public void Write(char ch)
    {
        _binaryWriter.Write(ch);
    }
    
    public void Write(char[] chars)
    {
        _binaryWriter.Write(chars);
    }
    
    public void Write(string value)
    {
        _binaryWriter.Write(value);
    }

    public void Write(bool value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(byte value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(byte[] buffer)
    {
        _binaryWriter.Write(buffer);
    }

    public void Write(sbyte value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(short value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(ushort value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(int value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(uint value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(long value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(ulong value)
    {
        _binaryWriter.Write(value);
    }

    public void Write7BitEncodedInt(int value)
    {
        _binaryWriter.Write7BitEncodedInt(value);
    }

    public void Write7BitEncodedInt64(long value)
    {
        _binaryWriter.Write7BitEncodedInt64(value);
    }
    
    public void Write(Half value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(float value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(double value)
    {
        _binaryWriter.Write(value);
    }
    
    public void Write(decimal value)
    {
        _binaryWriter.Write(value);
    }
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        return _memoryStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _memoryStream.SetLength(value);
    }
    
    public override void Flush()
    {
        _memoryStream.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _memoryStream.Dispose();
            _binaryReader.Dispose();
            _binaryWriter.Dispose();
        }

        base.Dispose(disposing);
    }
}
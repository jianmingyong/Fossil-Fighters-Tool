using System.Buffers;
using System.Text;
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

public abstract class CompressibleStream : Stream
{
    public override bool CanRead => Mode == CompressibleStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == CompressibleStreamMode.Compress && BaseStream.CanWrite;
    
    public override long Length => throw new NotSupportedException();
    
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    
    [PublicAPI]
    public Stream BaseStream { get; }
    
    [PublicAPI]
    public CompressibleStreamMode Mode { get; }
    
    private readonly MemoryStream? _inputStream;
    private readonly MemoryStream? _outputStream;
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;

    private bool _hasDecompressed;
    private bool _hasDisposed;

    protected CompressibleStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;
        
        if (mode == CompressibleStreamMode.Decompress)
        {
            if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
            
            _outputStream = new MemoryStream();
            _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen);
            _writer = new BinaryWriter(_outputStream);
        }
        else
        {
            if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));
            
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
        if (Mode == CompressibleStreamMode.Compress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));
        
        if (!_hasDecompressed)
        {
            if (BaseStream.CanSeek)
            {
                Decompress(_reader, _writer, BaseStream, _outputStream!);
            }
            else
            {
                using var inputStream = new MemoryStream();
                using var reader = new BinaryReader(inputStream);
                
                BaseStream.CopyTo(inputStream);
                inputStream.Seek(0, SeekOrigin.Begin);
                
                Decompress(reader, _writer, inputStream, _outputStream!);
            }
            
            _outputStream!.Seek(0, SeekOrigin.Begin);
            _hasDecompressed = true;
        }
        
        return _outputStream!.Read(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));
        
        _inputStream!.WriteByte(value);
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));
        
        _inputStream!.Write(buffer, offset, count);
    }
    
    public override void Flush()
    {
        if (Mode == CompressibleStreamMode.Decompress) return;
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));
        if (_inputStream!.Length == 0) return;
        
        _inputStream.Seek(0, SeekOrigin.Begin);
        
        using var outputStream = new MemoryStream();
        using var writer = new BinaryWriter(outputStream);
        
        Compress(_reader, writer, _inputStream!, outputStream);

        _inputStream.Seek(0, SeekOrigin.Begin);
        _inputStream.SetLength(0);

        outputStream.Seek(0, SeekOrigin.Begin);
        outputStream.CopyTo(_writer.BaseStream);
        
        _writer.Flush();
    }
    
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }
    
    protected abstract void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream);
    
    protected abstract void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_hasDisposed)
            {
                Flush();
            
                _reader.Dispose();
                _writer.Dispose();

                _hasDisposed = true;
            }
        }

        base.Dispose(disposing);
    }
}
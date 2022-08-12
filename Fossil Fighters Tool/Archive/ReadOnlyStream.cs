using System.Buffers;

namespace Fossil_Fighters_Tool.Archive;

public class ReadOnlyStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => false;
    
    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set
        {
            if (!CanSeek) throw new NotSupportedException();
            Seek(value, SeekOrigin.Begin);
        }
    }
    
    private readonly Stream _stream;
    private readonly long _offset;
    private readonly bool _leaveOpen;
    
    private long _position;
    
    public ReadOnlyStream(Stream stream) : this(stream, 0, stream.Length)
    {
    }

    public ReadOnlyStream(Stream stream, long offset, long length, bool leaveOpen = false)
    {
        if (!stream.CanRead) throw new ArgumentException(null, nameof(stream));

        _stream = stream;
        _offset = offset;
        _leaveOpen = leaveOpen;

        Length = length;

        if (offset <= 0) return;

        if (stream.CanSeek)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }
        else
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var totalBytesRead = 0L;
                
            try
            {
                do
                {
                    var bytesToRead = Math.Min(Math.Max(0, offset - totalBytesRead), 4096);
                    var bytesRead = stream.Read(buffer, 0, (int) bytesToRead);
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;
                } while (totalBytesRead < offset);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Length > 0)
        {
            // Stream has a specified length, ensure we don't go over the length.
            var bytesToRead = Math.Min(Length - _position, count);
            var bytesRead = _stream.Read(buffer, offset, (int) bytesToRead);
            _position += bytesRead;
            return bytesRead;
        }
        else
        {
            // Stream does not provide length or it is truly zero. Who knows?
            var bytesRead = _stream.Read(buffer, offset, count);
            _position += bytesRead;
            return bytesRead;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek) throw new NotSupportedException();
        
        switch (origin)
        {
            case SeekOrigin.Begin:
                if (offset > Length) throw new ArgumentException(null, nameof(offset));
                _stream.Seek(_offset + offset, SeekOrigin.Begin);
                _position = _offset;
                break;

            case SeekOrigin.Current:
                if (!CanSeek)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(4096);
                    var totalBytesRead = 0L;
                
                    try
                    {
                        do
                        {
                            var bytesToRead = Math.Min(Math.Max(0, offset - totalBytesRead), 4096);
                            var bytesRead = _stream.Read(buffer, 0, (int) bytesToRead);
                            if (bytesRead == 0) break;
                            totalBytesRead += bytesRead;
                        } while (totalBytesRead < offset);
                        
                        _position += totalBytesRead;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    if (_position + offset < 0 || _position + offset > Length) throw new ArgumentException(null, nameof(offset));
                    _stream.Seek(offset, SeekOrigin.Current);
                    _position += offset;
                }
                break;

            case SeekOrigin.End:
                if (offset > Length) throw new ArgumentException(null, nameof(offset));
                _stream.Seek(_offset + Length - offset, SeekOrigin.Begin);
                _position = _offset + Length - offset;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
        
        base.Dispose(disposing);
    }
}
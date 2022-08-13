using System.Text;

namespace Fossil_Fighters_Tool.Archive.Compression.Rle;

public class RleStream : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();
    
    public override long Position 
    { 
        get => throw new NotSupportedException();
        set => throw new NotSupportedException(); 
    }
    
    private readonly Stream _outputStream;
    private readonly RleStreamMode _mode;
    private readonly bool _leaveOpen;
    
    private readonly MemoryBinaryStream _inputStream;
    private long _inputStreamRead;
    private long _inputStreamWritten;

    // Decompress
    private bool _hasHeader;
    private bool _hasFlag;
    private int _decompressSize;
    private int _bytesWritten;
    private byte _flagDataLength;
    private byte _flagType;

    public RleStream(Stream outputStream, RleStreamMode mode, bool leaveOpen = false)
    {
        _inputStream = new MemoryBinaryStream(Encoding.ASCII);
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inputStream.Seek(_inputStreamWritten, SeekOrigin.Begin);
        _inputStream.Write(buffer, offset, count);
        _inputStream.Seek(_inputStreamRead, SeekOrigin.Begin);
        
        _inputStreamWritten += count;

        if (_mode == RleStreamMode.Decompress)
        {
            Decompress();
        }
        else
        {
            Compress();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException(); 
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException(); 
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException(); 
    }

    public override void Flush()
    {
        _outputStream.Flush();
    }

    private void Decompress()
    {
        if (!_hasHeader)
        {
            if (_inputStream.Length - _inputStream.Position < 4) return;

            var rawData = _inputStream.ReadUInt32();

            if (((rawData >> 4) & 0xF) != 3) throw new InvalidDataException("The contents of the stream is not in the rle format.");

            _decompressSize = (int) (rawData >> 8);
            _inputStreamRead += 4;
            
            _hasHeader = true;
        }

        if (_hasHeader)
        {
            do
            {
                if (!_hasFlag)
                {
                    if (_inputStream.Length - _inputStream.Position < 1) return;

                    var rawData = _inputStream.ReadByte();

                    _flagDataLength = (byte) (rawData & 0x7F);
                    _flagType = (byte) (rawData >> 7);
                    
                    _inputStreamRead += 1;
                    
                    _hasFlag = true;
                }

                if (_hasFlag)
                {
                    if (_flagType == 0)
                    {
                        var bytesToRead = _flagDataLength + 1;
                        if (_inputStream.Length - _inputStream.Position < bytesToRead) return;

                        for (var i = 0; i < bytesToRead; i++)
                        {
                            _outputStream.WriteByte(_inputStream.ReadByte());
                        }

                        _inputStreamRead += bytesToRead;
                        _bytesWritten += bytesToRead;
                    }
                    else
                    {
                        if (_inputStream.Length - _inputStream.Position < 1) return;

                        var dataToWrite = _inputStream.ReadByte();

                        for (var i = _flagDataLength + 3; i >= 0; i--)
                        {
                            _outputStream.WriteByte(dataToWrite);
                        }

                        _inputStreamRead += 1;
                        _bytesWritten += _flagDataLength + 3;
                    }

                    _hasFlag = false;
                }
            } while (_bytesWritten < _decompressSize);
            
            _outputStream.Flush();
        }
    }

    private void Compress()
    {
        throw new NotImplementedException();
    }
    
    protected override void Dispose(bool disposing)
    {
        if (!_leaveOpen)
        {
            _outputStream.Dispose();
        }
        
        _inputStream.Dispose();
        
        base.Dispose(disposing);
    }
}
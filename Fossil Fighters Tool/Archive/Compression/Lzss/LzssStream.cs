using System.Buffers;
using System.Text;

namespace Fossil_Fighters_Tool.Archive.Compression.Lzss;

public class LzssStream : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    private readonly MemoryStream _inputStream = new();
    private readonly BinaryReader _inputStreamReader;

    private readonly Stream _outputStream;
    private readonly LzssStreamMode _mode;
    private readonly bool _leaveOpen;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private long _inputStreamRead;
    private long _inputStreamWritten;

    // Decompress
    private bool _hasHeader;
    private bool _hasFlag;
    private bool _hasBlock;
    private int _decompressSize;
    private byte _flagData;
    private int _blockIndex;
    private int _bytesWritten;
    private MemoryStream? _tempOutputStream;

    public LzssStream(Stream outputStream, LzssStreamMode mode, bool leaveOpen = false)
    {
        _inputStreamReader = new BinaryReader(_inputStream, Encoding.ASCII);
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _inputStream.Seek(_inputStreamWritten, SeekOrigin.Begin);
        _inputStream.Write(buffer, offset, count);
        _inputStreamWritten += count;
        
        _inputStream.Seek(_inputStreamRead, SeekOrigin.Begin);

        if (_mode == LzssStreamMode.Decompress)
        {
            Decompress();
        }
        else
        {
            Compress();
        }
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

            var rawData = _inputStreamReader.ReadUInt32();
            _inputStreamRead += 4;

            if (((rawData >> 4) & 0xF) != 0x1) throw new InvalidDataException();

            _decompressSize = (int) (rawData >> 8);
            _tempOutputStream = new MemoryStream(_decompressSize);

            _hasHeader = true;
        }

        do
        {
            if (_hasHeader && !_hasFlag)
            {
                if (_inputStream.Length - _inputStream.Position < 1) return;

                _flagData = _inputStreamReader.ReadByte();
                _inputStreamRead += 1;

                _hasFlag = true;
                _hasBlock = false;
                _blockIndex = 7;
            }

            if (_hasHeader && _hasFlag && !_hasBlock)
            {
                for (; _blockIndex >= 0; _blockIndex--)
                {
                    var blockType = (_flagData >> _blockIndex) & 0x1;

                    if (blockType == 0)
                    {
                        if (_inputStream.Length - _inputStream.Position < 1) return;
                        _tempOutputStream!.WriteByte(_inputStreamReader.ReadByte());
                        _inputStreamRead += 1;
                        _bytesWritten += 1;
                    }
                    else
                    {
                        if (_inputStream.Length - _inputStream.Position < 2) return;
                        var rawData = _inputStreamReader.ReadByte();
                        var rawData2 = _inputStreamReader.ReadByte();
                        _inputStreamRead += 2;

                        var bytesToCopy = ((rawData >> 4) & 0xF) + 3;
                        var offset = ((rawData & 0xF) << 8) | rawData2;
                        var data = ArrayPool<byte>.Shared.Rent(bytesToCopy);
                        
                        try
                        {
                            _tempOutputStream!.Position -= offset + 1;

                            for (var i = 0; i < bytesToCopy; i++)
                            {
                                data[i] = (byte) _tempOutputStream.ReadByte();
                            }

                            _tempOutputStream.Seek(_bytesWritten, SeekOrigin.Begin);

                            for (var i = 0; i < bytesToCopy; i++)
                            {
                                _tempOutputStream.WriteByte(data[i]);
                                _bytesWritten += 1;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(data);
                        }
                    }

                    if (_bytesWritten == _decompressSize) break;
                }
                
                _hasBlock = true;
                _hasFlag = false;
            }
        } while (_bytesWritten < _decompressSize);
        
        _tempOutputStream!.Seek(0, SeekOrigin.Begin);
        _tempOutputStream.CopyTo(_outputStream);
    }

    private void Compress()
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (!_leaveOpen)
            {
                _outputStream.Dispose();
            }

            _inputStream.Dispose();
            _inputStreamReader.Dispose();
            _tempOutputStream?.Dispose();
        }

        base.Dispose(disposing);
    }
}
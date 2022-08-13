using System.Buffers;
using System.Text;

namespace Fossil_Fighters_Tool.Archive.Compression.Lzss;

public class LzssStream : Stream
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

    private const int LookbackBufferSize = 0xFFF + 1;
    
    private readonly Stream _outputStream;
    private readonly LzssStreamMode _mode;
    private readonly bool _leaveOpen;
    
    private readonly MemoryBinaryStream _inputStream;
    private long _inputStreamRead;
    private long _inputStreamWritten;
    
    // Decompress
    private bool _hasHeader;
    private bool _hasFlag;
    private bool _hasBlock;
    private int _decompressSize;
    private int _bytesWritten;
    private readonly byte[] _lookbackBuffer;
    private int _lookbackIndex;
    private byte _flagData;
    private int _blockIndex;
    
    public LzssStream(Stream outputStream, LzssStreamMode mode, bool leaveOpen = false)
    {
        _inputStream = new MemoryBinaryStream(Encoding.ASCII);
        _outputStream = outputStream;
        _mode = mode;
        _leaveOpen = leaveOpen;
        _lookbackBuffer = mode == LzssStreamMode.Decompress ? ArrayPool<byte>.Shared.Rent(LookbackBufferSize) : Array.Empty<byte>();
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inputStream.Seek(_inputStreamWritten, SeekOrigin.Begin);
        _inputStream.Write(buffer, offset, count);
        _inputStream.Seek(_inputStreamRead, SeekOrigin.Begin);
        
        _inputStreamWritten += count;

        if (_mode == LzssStreamMode.Decompress)
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
            if (((rawData >> 4) & 0xF) != 0x1) throw new InvalidDataException("The contents of the stream is not in the lzss format.");

            _decompressSize = (int) (rawData >> 8);
            _lookbackIndex = 0;
            
            _inputStreamRead += 4;

            _hasHeader = true;
        }

        do
        {
            if (_hasHeader && !_hasFlag)
            {
                if (_inputStream.Length - _inputStream.Position < 1) return;

                _flagData = _inputStream.ReadByte();
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

                        var byteToWrite = _inputStream.ReadByte();
                        
                        _outputStream.WriteByte(byteToWrite);
                        
                        _lookbackBuffer[_lookbackIndex++] = byteToWrite;
                        if (_lookbackIndex >= LookbackBufferSize) _lookbackIndex = 0;
                        
                        _inputStreamRead += 1;
                        _bytesWritten += 1;
                    }
                    else
                    {
                        if (_inputStream.Length - _inputStream.Position < 2) return;
                        
                        var rawData = _inputStream.ReadByte();
                        var rawData2 = _inputStream.ReadByte();
                        
                        var bytesToCopy = ((rawData >> 4) & 0xF) + 3;
                        var offset = (((rawData & 0xF) << 8) | rawData2) + 1;
                        
                        for (var i = 0; i < bytesToCopy; i++)
                        {
                            var index = _lookbackIndex - offset;

                            if (index < 0)
                            {
                                index += LookbackBufferSize;
                            }
                            else if (index >= LookbackBufferSize)
                            {
                                index -= LookbackBufferSize;
                            }
                            
                            _outputStream.WriteByte(_lookbackBuffer[index]);
                            
                            _lookbackBuffer[_lookbackIndex++] = _lookbackBuffer[index];
                            if (_lookbackIndex >= LookbackBufferSize) _lookbackIndex = 0;
                        }
                        
                        _inputStreamRead += 2;
                        _bytesWritten += bytesToCopy;
                    }

                    if (_bytesWritten == _decompressSize) break;
                }
                
                _hasBlock = true;
                _hasFlag = false;
            }
        } while (_bytesWritten < _decompressSize);
        
        _outputStream.Flush();
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

            if (_lookbackBuffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_lookbackBuffer);
            }
        }

        base.Dispose(disposing);
    }
}
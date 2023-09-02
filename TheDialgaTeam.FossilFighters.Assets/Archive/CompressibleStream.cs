// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2022 Yong Jian Ming
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

public abstract class CompressibleStream : Stream
{
    public override bool CanRead => Mode == CompressibleStreamMode.Decompress && BaseStream.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => Mode == CompressibleStreamMode.Compress && BaseStream.CanWrite;

    public override long Length => throw new NotSupportedException();

    public Stream BaseStream { get; }

    public CompressibleStreamMode Mode { get; }

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

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

    [SkipLocalsInit]
    public override int ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];
        return Read(buffer) > 0 ? buffer[0] : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        if (Mode == CompressibleStreamMode.Compress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_outputStream is not null);

        if (_hasDecompressed) return _outputStream.Read(buffer);

        if (BaseStream.CanSeek)
        {
            Decompress(_reader, _writer, BaseStream, _outputStream);
        }
        else
        {
            using var inputStream = new MemoryStream();
            using var reader = new BinaryReader(inputStream);

            BaseStream.CopyTo(inputStream);
            inputStream.Seek(0, SeekOrigin.Begin);

            Decompress(reader, _writer, inputStream, _outputStream);
        }

        _outputStream.Seek(0, SeekOrigin.Begin);
        _hasDecompressed = true;

        return _outputStream.Read(buffer);
    }

    public override void WriteByte(byte value)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_inputStream is not null);

        _inputStream.WriteByte(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_inputStream is not null);

        _inputStream.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_inputStream is not null);

        return _inputStream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_inputStream is not null);

        return _inputStream.WriteAsync(buffer, cancellationToken);
    }

    public override void Flush()
    {
        if (Mode == CompressibleStreamMode.Decompress) return;
        if (_hasDisposed) throw new ObjectDisposedException(nameof(CompressibleStream));

        Debug.Assert(_inputStream is not null);

        if (_inputStream.Length == 0) return;

        _inputStream.Seek(0, SeekOrigin.Begin);

        using var outputStream = new MemoryStream();
        using var writer = new BinaryWriter(outputStream);

        Compress(_reader, writer, _inputStream, outputStream);

        _inputStream.Seek(0, SeekOrigin.Begin);
        _inputStream.SetLength(0);

        outputStream.WriteTo(BaseStream);

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
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

using System.Buffers;
using TheDialgaTeam.FossilFighters.Assets.Archive.Compression;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

public record struct McmFileMetadata(
    uint MaxSizePerChunk,
    McmFileCompressionType CompressionType1,
    McmFileCompressionType CompressionType2);

public sealed class McmFileStream : CompressibleStream
{
    public uint MaxSizePerChunk
    {
        get => _maxSizePerChunk;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _maxSizePerChunk = value;
        }
    }

    public McmFileCompressionType CompressionType1
    {
        get => _compressionType1;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _compressionType1 = value;
        }
    }

    public McmFileCompressionType CompressionType2
    {
        get => _compressionType2;
        set
        {
            if (Mode == CompressibleStreamMode.Decompress) throw new NotSupportedException();
            _compressionType2 = value;
        }
    }

    private const uint HeaderId = 0x004D434D;

    private uint _maxSizePerChunk = 0x2000;
    private McmFileCompressionType _compressionType1 = McmFileCompressionType.None;
    private McmFileCompressionType _compressionType2 = McmFileCompressionType.None;

    public McmFileStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    public McmFileMetadata GetFileMetadata()
    {
        return new McmFileMetadata(MaxSizePerChunk, CompressionType1, CompressionType2);
    }

    public void LoadMetadata(McmFileMetadata metadata)
    {
        MaxSizePerChunk = metadata.MaxSizePerChunk;
        CompressionType1 = metadata.CompressionType1;
        CompressionType2 = metadata.CompressionType2;
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer)
    {
        if (writer.BaseStream is not MemoryStream outputStream) throw new ArgumentException(null, nameof(writer));

        var fileHeaderId = reader.ReadUInt32();
        if (fileHeaderId != HeaderId) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "MCM"));

        var decompressFileSize = reader.ReadUInt32();
        _maxSizePerChunk = reader.ReadUInt32();
        var numberOfChunk = reader.ReadUInt32();

        var dataChunkOffsets = new uint[numberOfChunk + 1];
        _compressionType1 = (McmFileCompressionType) reader.ReadByte();
        _compressionType2 = (McmFileCompressionType) reader.ReadByte();

        reader.BaseStream.Seek(2, SeekOrigin.Current);

        for (var i = 0; i < dataChunkOffsets.Length; i++)
        {
            dataChunkOffsets[i] = reader.ReadUInt32();
        }

        for (var i = 0; i < dataChunkOffsets.Length - 1; i++)
        {
            var requiredLength = dataChunkOffsets[i + 1] - dataChunkOffsets[i];
            var tempBuffer = ArrayPool<byte>.Shared.Rent((int) requiredLength);

            try
            {
                Stream dataChunk = new MemoryStream(tempBuffer, 0, (int) requiredLength);

                reader.BaseStream.Seek(dataChunkOffsets[i], SeekOrigin.Begin);
                if (reader.Read(tempBuffer, 0, (int) requiredLength) < requiredLength) throw new EndOfStreamException();

                var compressedStream = CompressionType1 switch
                {
                    McmFileCompressionType.None => dataChunk,
                    McmFileCompressionType.Rle => new RleStream(dataChunk, CompressibleStreamMode.Decompress),
                    McmFileCompressionType.Lzss => new LzssStream(dataChunk, CompressibleStreamMode.Decompress),
                    McmFileCompressionType.Huffman => new HuffmanStream(dataChunk, CompressibleStreamMode.Decompress),
                    var _ => throw new ArgumentOutOfRangeException(null)
                };

                switch (CompressionType2)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressedStream = new RleStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressedStream = new LzssStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressedStream = new HuffmanStream(compressedStream, CompressibleStreamMode.Decompress);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(null);
                }

                compressedStream.CopyTo(outputStream);
                compressedStream.Dispose();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        if (outputStream.Length != decompressFileSize) throw new InvalidDataException(Localization.StreamIsCorrupted);
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer)
    {
        if (reader.BaseStream is not MemoryStream inputStream) throw new ArgumentException(null, nameof(reader));
        if (writer.BaseStream is not MemoryStream outputStream) throw new ArgumentException(null, nameof(writer));

        writer.Write(HeaderId);
        writer.Write((uint) inputStream.Length);
        writer.Write(MaxSizePerChunk);

        var numberOfChunks = (uint) Math.Ceiling((double) inputStream.Length / MaxSizePerChunk);
        writer.Write(numberOfChunks);
        writer.Write((byte) CompressionType1);
        writer.Write((byte) CompressionType2);
        writer.Write((short) 0);

        var chunkOffset = outputStream.Position;
        var dataOffset = chunkOffset + 4 * numberOfChunks + 4;

        writer.Write((int) dataOffset);

        chunkOffset += 4;

        for (var i = 0; i < numberOfChunks; i++)
        {
            var chunkSize = Math.Min(inputStream.Length - MaxSizePerChunk * i, MaxSizePerChunk);
            var buffer = ArrayPool<byte>.Shared.Rent((int) chunkSize);

            try
            {
                using var tempBuffer = new MemoryStream();

                Stream compressStream = CompressionType1 switch
                {
                    McmFileCompressionType.None => tempBuffer,
                    McmFileCompressionType.Rle => new RleStream(tempBuffer, CompressibleStreamMode.Compress, true),
                    McmFileCompressionType.Lzss => new LzssStream(tempBuffer, CompressibleStreamMode.Compress, true),
                    McmFileCompressionType.Huffman => new HuffmanStream(tempBuffer, CompressibleStreamMode.Compress, true),
                    var _ => throw new ArgumentOutOfRangeException()
                };

                switch (CompressionType2)
                {
                    case McmFileCompressionType.None:
                        break;

                    case McmFileCompressionType.Rle:
                        compressStream = new RleStream(compressStream, CompressibleStreamMode.Compress);
                        break;

                    case McmFileCompressionType.Lzss:
                        compressStream = new LzssStream(compressStream, CompressibleStreamMode.Compress);
                        break;

                    case McmFileCompressionType.Huffman:
                        compressStream = new HuffmanStream(compressStream, CompressibleStreamMode.Compress);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                using var stream = new MemoryStream(buffer, 0, (int) chunkSize);
                if (reader.Read(buffer, 0, (int) chunkSize) < chunkSize) throw new EndOfStreamException();

                stream.WriteTo(compressStream);
                if (compressStream is not MemoryStream) compressStream.Dispose();

                var dataLength = tempBuffer.Length;

                writer.Seek((int) chunkOffset + 4 * i, SeekOrigin.Begin);
                writer.Write((int) (dataOffset + dataLength));

                writer.Seek((int) dataOffset, SeekOrigin.Begin);
                tempBuffer.WriteTo(outputStream);

                dataOffset += dataLength;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
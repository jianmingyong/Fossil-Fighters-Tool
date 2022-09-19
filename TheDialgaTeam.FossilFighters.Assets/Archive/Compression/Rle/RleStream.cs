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
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Archive.Compression.Rle;

[PublicAPI]
public sealed class RleStream : CompressibleStream
{
    private const int CompressionHeader = 3 << 4;

    private const int CompressFlag = 1 << 7;

    private const int MaxFlagDataLength = CompressFlag - 1;

    private const int MinUncompressDataLength = 1;
    private const int MaxRawDataLength = MaxFlagDataLength + MinUncompressDataLength;
    private const int MinCompressDataLength = 3;
    private const int MaxCompressDataLength = MaxFlagDataLength + MinCompressDataLength;

    public RleStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream)
    {
        var rawHeaderData = reader.ReadInt32();
        if ((rawHeaderData & CompressionHeader) != CompressionHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "RLE"));

        var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;
        outputStream.Capacity = decompressSize;

        while (outputStream.Length < decompressSize)
        {
            var flagRawData = reader.ReadByte();
            var flagType = flagRawData >> 7;
            var flagData = flagRawData & MaxFlagDataLength;

            if (flagType == 0)
            {
                var repeatCount = flagData + 1;

                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(reader.ReadByte());
                }
            }
            else
            {
                var repeatCount = flagData + 3;
                var repeatData = reader.ReadByte();

                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(repeatData);
                }
            }
        }
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream)
    {
        int GetNextRepeatCount(Span<byte> buffer)
        {
            if (inputStream.Position >= inputStream.Length) return 0;

            var bytesWritten = 0;
            var dataToCheck = reader.ReadByte();

            buffer[bytesWritten++] = dataToCheck;

            while (bytesWritten < MaxCompressDataLength && inputStream.Position < inputStream.Length)
            {
                if (dataToCheck != reader.ReadByte())
                {
                    inputStream.Seek(-1, SeekOrigin.Current);
                    break;
                }

                buffer[bytesWritten++] = dataToCheck;
            }

            return bytesWritten;
        }

        void WriteCompressed(byte data, int count)
        {
            writer.Write((byte) (CompressFlag | (count - MinCompressDataLength)));
            writer.Write(data);
        }

        void WriteUncompressed(ReadOnlySpan<byte> buffer)
        {
            writer.Write((byte) (buffer.Length - MinUncompressDataLength));
            writer.Write(buffer);
        }

        writer.Write((uint) (CompressionHeader | (inputStream.Length << 8)));

        var tempBuffer = ArrayPool<byte>.Shared.Rent(MaxCompressDataLength);
        var rawDataBuffer = ArrayPool<byte>.Shared.Rent(MaxRawDataLength);

        try
        {
            int tempBufferLength;
            var rawDataLength = 0;

            while ((tempBufferLength = GetNextRepeatCount(tempBuffer)) > 0)
            {
                if (tempBufferLength >= MinCompressDataLength)
                {
                    if (rawDataLength > 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;
                    }

                    WriteCompressed(tempBuffer[0], tempBufferLength);
                }
                else
                {
                    var rawDataSpaceRemaining = MaxRawDataLength - rawDataLength;

                    if (rawDataSpaceRemaining == 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;

                        tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                        rawDataLength += tempBufferLength;
                    }
                    else
                    {
                        if (tempBufferLength > rawDataSpaceRemaining)
                        {
                            tempBuffer.AsSpan(0, rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;

                            WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                            rawDataLength = 0;

                            tempBuffer.AsSpan(rawDataSpaceRemaining, tempBufferLength - rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += tempBufferLength - rawDataSpaceRemaining;
                        }
                        else
                        {
                            tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;
                        }
                    }
                }
            }

            if (rawDataLength > 0)
            {
                WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
            }

            while (outputStream.Length % 4 != 0)
            {
                writer.Write((byte) 0);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
            ArrayPool<byte>.Shared.Return(rawDataBuffer);
        }
    }
}
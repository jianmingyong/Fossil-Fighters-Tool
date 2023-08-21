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

using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Archive.Compression;

[PublicAPI]
public sealed class RleStream : CompressibleStream
{
    private const int CompressionHeader = 3 << 4;

    private const int CompressFlag = 1 << 7;

    private const int MaxFlagDataLength = CompressFlag - 1;

    private const int MinRawDataLength = 1;
    private const int MaxRawDataLength = MaxFlagDataLength + MinRawDataLength;
    private const int MinCompressDataLength = 3;
    private const int MaxCompressDataLength = MaxFlagDataLength + MinCompressDataLength;

    private const int MaxInputDataLength = (1 << 24) - 1;

    public RleStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream)
    {
        var rawHeaderData = reader.ReadInt32();
        if ((rawHeaderData & 0xF0) != CompressionHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "RLE"));

        var decompressSize = rawHeaderData >>> 8;

        if (outputStream.Capacity < decompressSize)
        {
            outputStream.Capacity = decompressSize;
        }

        while (outputStream.Length < decompressSize)
        {
            var flagRawData = reader.ReadByte();
            var flagType = (flagRawData >> 7) & 0x1;
            var flagData = flagRawData & MaxFlagDataLength;

            if (flagType == 0)
            {
                var repeatCount = flagData + 1;

                if (outputStream.Length + repeatCount > decompressSize) throw new InvalidDataException(Localization.StreamIsCorrupted);

                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(reader.ReadByte());
                }
            }
            else
            {
                var repeatCount = flagData + 3;
                var repeatData = reader.ReadByte();

                if (outputStream.Length + repeatCount > decompressSize) throw new InvalidDataException(Localization.StreamIsCorrupted);

                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(repeatData);
                }
            }
        }
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream)
    {
        var dataLength = inputStream.Length;
        if (dataLength > MaxInputDataLength) throw new InvalidDataException(string.Format(Localization.StreamDataTooLarge, "RLE"));

        writer.Write((uint) (CompressionHeader | (dataLength << 8)));

        Span<byte> tempBuffer = stackalloc byte[MaxCompressDataLength];
        Span<byte> rawDataBuffer = stackalloc byte[MaxRawDataLength];

        int tempBufferLength;
        var rawDataLength = 0;

        while ((tempBufferLength = GetNextRepeatCount(tempBuffer)) > 0)
        {
            if (tempBufferLength >= MinCompressDataLength)
            {
                if (rawDataLength > 0)
                {
                    WriteUncompressed(rawDataBuffer[..rawDataLength]);
                    rawDataLength = 0;
                }

                WriteCompressed(tempBuffer[0], tempBufferLength);
            }
            else
            {
                var rawDataSpaceRemaining = MaxRawDataLength - rawDataLength;

                if (rawDataSpaceRemaining == 0)
                {
                    WriteUncompressed(rawDataBuffer[..rawDataLength]);
                    rawDataLength = 0;

                    tempBuffer[..tempBufferLength].CopyTo(rawDataBuffer[rawDataLength..]);
                    rawDataLength += tempBufferLength;
                }
                else if (tempBufferLength <= rawDataSpaceRemaining)
                {
                    tempBuffer[..tempBufferLength].CopyTo(rawDataBuffer[rawDataLength..]);
                    rawDataLength += tempBufferLength;
                }
                else
                {
                    tempBuffer[..rawDataSpaceRemaining].CopyTo(rawDataBuffer[rawDataLength..]);
                    rawDataLength += rawDataSpaceRemaining;

                    WriteUncompressed(rawDataBuffer[..rawDataLength]);
                    rawDataLength = 0;

                    var remainingDataToCopy = tempBufferLength - rawDataSpaceRemaining;
                    tempBuffer.Slice(rawDataSpaceRemaining, remainingDataToCopy).CopyTo(rawDataBuffer[rawDataLength..]);
                    rawDataLength += remainingDataToCopy;
                }
            }
        }

        if (rawDataLength > 0)
        {
            WriteUncompressed(rawDataBuffer[..rawDataLength]);
        }

        while (outputStream.Length % 4 != 0)
        {
            writer.Write((byte) 0);
        }

        return;

        void WriteCompressed(byte data, int count)
        {
            writer.Write((byte) (CompressFlag | (count - MinCompressDataLength)));
            writer.Write(data);
        }

        void WriteUncompressed(ReadOnlySpan<byte> buffer)
        {
            writer.Write((byte) (buffer.Length - MinRawDataLength));
            writer.Write(buffer);
        }

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
    }
}
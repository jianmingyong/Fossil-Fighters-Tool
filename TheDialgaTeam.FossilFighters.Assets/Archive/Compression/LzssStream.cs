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

namespace TheDialgaTeam.FossilFighters.Assets.Archive.Compression;

public sealed class LzssStream : CompressibleStream
{
    private const int CompressHeader = 0x10;

    private const int MinDisplacement = 0x1 + 1;
    private const int MaxDisplacement = 0xFFF + 1;

    private const int MinBytesToCopy = 3;
    private const int MaxBytesToCopy = 0xF + MinBytesToCopy;

    private const int MaxInputDataLength = (1 << 24) - 1;

    public LzssStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }

    protected override void Decompress(BinaryReader reader, BinaryWriter writer)
    {
        if (writer.BaseStream is not MemoryStream outputStream) throw new ArgumentException(null, nameof(writer));

        var rawHeaderData = reader.ReadUInt32();
        if ((rawHeaderData & 0xF0) != CompressHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "LZ77"));

        var decompressSize = rawHeaderData >> 8;

        while (outputStream.Length < decompressSize)
        {
            var flagData = reader.ReadByte();

            for (var i = 7; i >= 0; i--)
            {
                var blockType = (flagData >> i) & 0x1;

                if (blockType == 0)
                {
                    writer.Write(reader.ReadByte());
                }
                else
                {
                    var compressHeader = reader.ReadUInt16();
                    var copyCount = ((compressHeader >> 4) & 0xF) + 3;
                    var displacement = (((compressHeader & 0xF) << 8) | ((compressHeader >> 8) & 0xFF)) + 1;
                    var buffer = outputStream.GetBuffer().AsSpan((int) (outputStream.Position - displacement));

                    if (copyCount + outputStream.Length > decompressSize) throw new InvalidDataException(Localization.StreamIsCorrupted);

                    for (var j = 0; j < copyCount; j++)
                    {
                        writer.Write(buffer[j % buffer.Length]);
                    }
                }

                if (outputStream.Length >= decompressSize) break;
            }
        }
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer)
    {
        if (reader.BaseStream is not MemoryStream inputStream) throw new ArgumentException(null, nameof(reader));
        if (writer.BaseStream is not MemoryStream outputStream) throw new ArgumentException(null, nameof(writer));

        var dataLength = inputStream.Length;
        if (dataLength > MaxInputDataLength) throw new InvalidDataException(string.Format(Localization.StreamDataTooLarge, "LZ77"));

        writer.Write((uint) (CompressHeader | (dataLength << 8)));

        Span<byte> tempBuffer = stackalloc byte[16];
        var tempBufferSize = 0;

        var flagData = 0;
        var flagIndex = 7;

        while (inputStream.Position < inputStream.Length)
        {
            var startIndex = Math.Max(0, inputStream.Position - MaxDisplacement);
            var length = inputStream.Position - startIndex;

            var nextToken = SearchForNextToken(inputStream.GetBuffer().AsSpan((int) startIndex, (int) length));

            if (nextToken.displacement < MinDisplacement || nextToken.bytesToCopy < MinBytesToCopy)
            {
                var dataToWrite = reader.ReadByte();
                tempBuffer[tempBufferSize++] = dataToWrite;
            }
            else
            {
                var newDisplacement = nextToken.displacement - 1;
                var newBytesToCopy = nextToken.bytesToCopy - 3;
                tempBuffer[tempBufferSize++] = (byte) (((newDisplacement & 0xF00) >> 8) | (newBytesToCopy << 4));
                tempBuffer[tempBufferSize++] = (byte) (newDisplacement & 0xFF);

                flagData |= 1 << flagIndex;
            }

            flagIndex--;

            if (flagIndex >= 0) continue;

            writer.Write((byte) flagData);
            writer.Write(tempBuffer[..tempBufferSize]);

            tempBufferSize = 0;
            flagData = 0;
            flagIndex = 7;
        }

        if (flagIndex < 7)
        {
            writer.Write((byte) flagData);
            writer.Write(tempBuffer[..tempBufferSize]);
        }

        while (outputStream.Length % 4 != 0)
        {
            writer.Write((byte) 0);
        }

        return;

        (int displacement, int bytesToCopy) SearchForNextToken(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < MinDisplacement) return (0, 0);

            var searchOffset = inputStream.Position;
            var biggestDisplacement = 0;
            var biggestToCopy = 0;

            for (var i = buffer.Length - MinDisplacement; i >= 0; i--)
            {
                inputStream.Seek(searchOffset, SeekOrigin.Begin);

                var repeatCount = GetNextRepeatCount(buffer[i..]);
                if (repeatCount < MinBytesToCopy) continue;
                if (repeatCount <= biggestToCopy) continue;

                biggestToCopy = repeatCount;
                biggestDisplacement = buffer.Length - i;
            }

            inputStream.Seek(searchOffset + biggestToCopy, SeekOrigin.Begin);

            return (biggestDisplacement, biggestToCopy);
        }

        int GetNextRepeatCount(ReadOnlySpan<byte> buffer)
        {
            if (inputStream.Position >= inputStream.Length) return 0;

            var bufferIndex = 0;

            while (bufferIndex < MaxBytesToCopy && inputStream.Position < inputStream.Length)
            {
                if (buffer[bufferIndex % buffer.Length] != reader.ReadByte()) break;
                bufferIndex++;
            }

            return bufferIndex;
        }
    }
}
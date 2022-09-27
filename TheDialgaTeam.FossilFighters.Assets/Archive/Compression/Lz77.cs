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

using System.Buffers.Binary;

namespace TheDialgaTeam.FossilFighters.Assets.Archive.Compression;

public static class Lz77
{
    private const int CompressHeader = 0x10;

    private const int MinDisplacement = 0x1 + 1;
    private const int MaxDisplacement = 0xFFF + 1;

    private const int MinBytesToCopy = 3;
    private const int MaxBytesToCopy = 0xF + MinBytesToCopy;

    public static int Compress(ReadOnlySpan<byte> input, Span<byte> output)
    {
        return 0;
    }

    public static int Decompress(ReadOnlySpan<byte> input, Span<byte> output)
    {
        var decompressSize = GetDecompressSize(input);
        if (output.Length < decompressSize) throw new ArgumentOutOfRangeException(nameof(output), "output is too small to contain the full decompressed Lz77 data.");

        var read = 4;
        var written = 0;

        while (written < decompressSize)
        {
            var flagData = input[read++];

            for (var i = 7; i >= 0; i--)
            {
                var blockType = (flagData >> i) & 0x1;

                if (blockType == 0)
                {
                    output[written++] = input[read++];
                }
                else
                {
                    var compressHeader = BinaryPrimitives.ReadUInt16LittleEndian(input[read..]);
                    var copyCount = ((compressHeader >> 4) & 0xF) + 3;
                    var displacement = (((compressHeader & 0xF) << 8) | ((compressHeader >> 8) & 0xFF)) + 1;
                    var buffer = output[(written - displacement)..];
                    var bufferLength = buffer.Length;

                    if (copyCount + written > decompressSize) throw new ArgumentException("input data contains invalid copy count.", nameof(input));

                    for (var j = 0; j < copyCount; j++)
                    {
                        output[written++] = buffer[j % bufferLength];
                    }

                    read += sizeof(ushort);
                }

                if (written == decompressSize) break;
            }
        }

        return written;
    }

    public static int GetDecompressSize(ReadOnlySpan<byte> input)
    {
        var dataHeader = BinaryPrimitives.ReadInt32LittleEndian(input);
        if ((dataHeader & 0xF) != CompressHeader) throw new ArgumentException("input data is not LZ77 compressed.", nameof(input));
        return dataHeader >>> 8;
    }
}
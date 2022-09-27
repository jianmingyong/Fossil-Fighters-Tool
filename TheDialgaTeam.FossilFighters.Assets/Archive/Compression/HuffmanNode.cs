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

public sealed class HuffmanNode
{
    public HuffmanNode? Parent { get; set; }

    public HuffmanNode? Left { get; set; }

    public HuffmanNode? Right { get; set; }

    public byte? Data { get; set; }

    public int Value { get; set; }

    public long Position { get; set; } = 5;

    public int BitstreamValue { get; set; }

    public int BitstreamLength { get; set; }

    public HuffmanNode()
    {
    }

    public HuffmanNode(BinaryReader reader, long position, long endPosition, HuffmanDataSize dataSize, bool isData)
    {
        reader.BaseStream.Seek(position, SeekOrigin.Begin);

        var rawByte = reader.ReadByte();

        if (isData)
        {
            if (dataSize == HuffmanDataSize.FourBits && (rawByte & 0xF0) > 0) throw new InvalidDataException(Localization.HuffmanStreamInvalidDataNode);
            Data = rawByte;
        }
        else
        {
            var offset = rawByte & 0x3F;

            var leftOffset = (position & ~1L) + offset * 2 + 2;
            var rightOffset = (position & ~1L) + offset * 2 + 2 + 1;

            if (leftOffset < endPosition)
            {
                Left = new HuffmanNode(reader, leftOffset, endPosition, dataSize, (rawByte & 0x80) > 0);
            }

            if (rightOffset < endPosition)
            {
                Right = new HuffmanNode(reader, rightOffset, endPosition, dataSize, (rawByte & 0x40) > 0);
            }
        }
    }
}
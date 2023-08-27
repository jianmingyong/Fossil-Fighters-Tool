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

using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

public sealed class DalHeader
{
    public const int FileHeader = 0x004C4144;

    public int Unknown1 { get; init; }

    public int AttackOffsetCount { get; init; }

    public int StartingOffset { get; init; }

    private DalHeader()
    {
    }

    public static DalHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DAL"));

        var unknown1 = reader.ReadInt32();
        var offsetCount = reader.ReadInt32();
        var startingOffset = reader.ReadInt32();

        stream.Seek(startingOffset, SeekOrigin.Begin);

        var offsets = new int[offsetCount];

        for (var i = 0; i < offsetCount; i++)
        {
            offsets[i] = reader.ReadInt32();
        }

        var data = new List<byte[]>();

        for (var i = 0; i < offsetCount; i++)
        {
            var startDataOffset = offsets[i];
            var endDataOffset = i == offsetCount - 1 ? stream.Length : offsets[i + 1];

            stream.Seek(startDataOffset, SeekOrigin.Begin);
            data.Add(reader.ReadBytes((int) (endDataOffset - startDataOffset)));
        }

        return new DalHeader();
    }
}
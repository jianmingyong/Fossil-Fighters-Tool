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

public sealed class DtxHeader
{
    public const int FileHeader = 0x00585444;

    public uint TextCount { get; }

    public uint StartingOffset { get; } = 0xC;

    public string[] Texts { get; }

    public DtxHeader(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DTX"));

        TextCount = reader.ReadUInt32();
        Texts = new string[TextCount];

        stream.Seek(reader.ReadUInt32(), SeekOrigin.Begin);

        var textOffsets = new uint[TextCount];

        for (var i = 0; i < TextCount; i++)
        {
            textOffsets[i] = reader.ReadUInt32();
        }

        for (var i = 0; i < TextCount; i++)
        {
            stream.Seek(textOffsets[i], SeekOrigin.Begin);

            var textBuilder = new StringBuilder();
            char nextChar;

            while ((nextChar = reader.ReadChar()) != '\0')
            {
                textBuilder.Append(nextChar);
            }

            Texts[i] = textBuilder.ToString();
        }
    }
}
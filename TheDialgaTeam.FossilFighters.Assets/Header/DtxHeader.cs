﻿// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
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
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

[PublicAPI]
public sealed class DtxHeader
{
    public const int FileHeader = 0x00585444;

    public string[] Texts { get; }

    public DtxHeader(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DTX"));

        var textCount = reader.ReadInt32();
        Texts = new string[textCount];

        stream.Seek(reader.ReadInt32(), SeekOrigin.Begin);

        var textOffsets = new int[textCount];

        for (var i = 0; i < textCount; i++)
        {
            textOffsets[i] = reader.ReadInt32();
        }

        for (var i = 0; i < textCount; i++)
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
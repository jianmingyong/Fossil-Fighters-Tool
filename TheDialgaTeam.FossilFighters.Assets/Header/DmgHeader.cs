// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2023 Yong Jian Ming
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
using System.Text.Json.Serialization;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

public readonly record struct DmgTextInfo(int TextId, string Text);

[JsonSerializable(typeof(DmgHeader), GenerationMode = JsonSourceGenerationMode.Serialization)]
public sealed partial class DmgHeaderContext : JsonSerializerContext
{
}

public sealed class DmgHeader
{
    public const int FileHeader = 0x00474D44;

    public DmgTextInfo[] Texts { get; init; }

    public DmgHeader(DmgTextInfo[] texts)
    {
        Texts = texts;
    }

    public static DmgHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DMG"));

        var textCount = reader.ReadUInt32();
        var texts = new DmgTextInfo[textCount];

        stream.Seek(reader.ReadUInt32(), SeekOrigin.Begin);

        var textOffsets = new uint[textCount];

        for (var i = 0; i < textCount; i++)
        {
            textOffsets[i] = reader.ReadUInt32();
        }

        for (var i = 0; i < textCount; i++)
        {
            stream.Seek(textOffsets[i], SeekOrigin.Begin);

            var textBuilder = new StringBuilder();
            char nextChar;

            var id = reader.ReadInt32();
            _ = reader.ReadInt32();

            while ((nextChar = reader.ReadChar()) != '\0')
            {
                textBuilder.Append(nextChar);
            }

            texts[i] = new DmgTextInfo(id, textBuilder.ToString());
        }

        return new DmgHeader(texts);
    }
}
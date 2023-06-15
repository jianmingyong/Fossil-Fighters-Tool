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
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

[JsonSerializable(typeof(DmgHeader))]
public partial class DmgHeaderContext : JsonSerializerContext
{
    
}

public readonly struct DmgTextInfo
{
    public int TextId { get; init; }

    public int Unknown2 { get; init; }
    
    public string Text { get; init; }
}

[PublicAPI]
public sealed class DmgHeader
{
    public const int FileHeader = 0x00474D44;

    public DmgTextInfo[] Texts { get; init; }
    
    private DmgHeader()
    {
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
            int id1, id2 = 0;
            char nextChar;

            id1 = reader.ReadInt32();
            id2 = reader.ReadInt32();
            
            while ((nextChar = reader.ReadChar()) != '\0')
            {
                textBuilder.Append(nextChar);
            }

            texts[i] = new DmgTextInfo { TextId = id1, Unknown2 = id2, Text = textBuilder.ToString() };
        }

        return new DmgHeader { Texts = texts };
    }
}
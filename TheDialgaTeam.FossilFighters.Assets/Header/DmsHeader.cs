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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DmsHeader))]
public sealed partial class DmsHeaderContext : JsonSerializerContext
{
}

public sealed class DmsHeader
{
    public const int FileHeader = 0x00534D44;
    
    public int Value { get; set; }

    public static DmsHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DMS"));

        return new DmsHeader
        {
            Value = reader.ReadInt32()
        };
    }
    
    public string ToJsonString()
    {
        return JsonSerializer.Serialize(this, DmsHeaderContext.Default.DmsHeader);
    }
}
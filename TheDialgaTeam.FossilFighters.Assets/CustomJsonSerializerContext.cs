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

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.GameData;

namespace TheDialgaTeam.FossilFighters.Assets;

[JsonSerializable(typeof(DmgFile))]
[JsonSerializable(typeof(DtxFile))]
[JsonSerializable(typeof(Dictionary<int, McmFileMetadata>))]
public sealed partial class CustomJsonSerializerContext : JsonSerializerContext
{
    public static CustomJsonSerializerContext Custom => _custom ??= new CustomJsonSerializerContext(CustomOptions);

    private static readonly JsonSerializerOptions CustomOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static CustomJsonSerializerContext? _custom;
}
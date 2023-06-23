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
[JsonSerializable(typeof(AclHeader))]
public sealed partial class AclHeaderContext : JsonSerializerContext
{
}

public record DlcInfo(ushort Index, ushort Id);

public sealed class AclHeader
{
    public const int FileHeader = 0x004C4341;

    public DlcInfo[] DlcInfos { get; set; } = Array.Empty<DlcInfo>();
    
    private AclHeader()
    {
    }

    public static AclHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "ACL"));

        var dlcCount = reader.ReadInt32();
        var offset = reader.ReadInt32();
        var dlcInfos = new DlcInfo[dlcCount];
        
        reader.BaseStream.Seek(offset, SeekOrigin.Begin);
        
        for (var i = 0; i < dlcCount; i++)
        {
            dlcInfos[i] = new DlcInfo(reader.ReadUInt16(), reader.ReadUInt16());
        }

        return new AclHeader
        {
            DlcInfos = dlcInfos
        };
    }
    
    public string ToJsonString()
    {
        return JsonSerializer.Serialize(this, AclHeaderContext.Default.AclHeader);
    }
}
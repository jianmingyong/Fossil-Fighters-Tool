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
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Header;

[PublicAPI]
public readonly struct BpExchangeInfo
{
    public ushort BpValue { get; }

    public ushort DpCost { get; }

    public BpExchangeInfo(ushort bpValue, ushort dpCost)
    {
        BpValue = bpValue;
        DpCost = dpCost;
    }
}

[PublicAPI]
public sealed class DbtlHeader
{
    public const int FileHeader = 0x4C544244;

    public BpExchangeInfo[] BpExchangeInfos { get; }

    public DbtlHeader(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DBTL"));

        stream.Seek(0x04, SeekOrigin.Current);

        BpExchangeInfos = new BpExchangeInfo[reader.ReadUInt32()];

        stream.Seek(reader.ReadUInt32(), SeekOrigin.Begin);

        for (var i = 0; i < BpExchangeInfos.Length; i++)
        {
            BpExchangeInfos[i] = new BpExchangeInfo(reader.ReadUInt16(), reader.ReadUInt16());
        }
    }
}
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
public class MpmHeader
{
    public const int FileHeader = 0x004D504D;

    public int Unknown1 { get; init; }

    public int Unknown2 { get; init; }

    public int Unknown3 { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int Unknown4 { get; init; }

    public int Unknown5 { get; init; }

    public int Unknown6 { get; init; }

    public int ColorPaletteFileIndex { get; init; }

    public string ColorPaletteFileName { get; init; } = string.Empty;

    public int BitmapFileIndex { get; init; }

    public string BitmapFileName { get; init; } = string.Empty;

    public int BgMapFileIndex { get; init; }

    public string BgMapFileName { get; init; } = string.Empty;

    public static MpmHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "MPM"));

        var unknown1 = reader.ReadInt32();
        var unknown2 = reader.ReadInt32();
        var unknown3 = reader.ReadInt32();
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var unknown4 = reader.ReadInt32();
        var unknown5 = reader.ReadInt32();
        var unknown6 = reader.ReadInt32();
        var colorPaletteFileIndex = reader.ReadInt32();
        var colorPaletteFileOffset = reader.ReadInt32();
        var bitmapFileIndex = reader.ReadInt32();
        var bitmapFileOffset = reader.ReadInt32();
        var bgMapFileIndex = reader.ReadInt32();
        var unknown8 = reader.ReadInt32();

        var colorPaletteFile = new StringBuilder();
        reader.BaseStream.Seek(colorPaletteFileOffset, SeekOrigin.Begin);

        while (reader.PeekChar() != '\0')
        {
            colorPaletteFile.Append(reader.ReadChar());
        }

        var bitmapFile = new StringBuilder();
        reader.BaseStream.Seek(bitmapFileOffset, SeekOrigin.Begin);

        while (reader.PeekChar() != '\0')
        {
            bitmapFile.Append(reader.ReadChar());
        }

        var bgMapFile = new StringBuilder();

        if (unknown8 != 0)
        {
            reader.BaseStream.Seek(bitmapFileOffset, SeekOrigin.Begin);

            while (reader.PeekChar() != '\0')
            {
                bgMapFile.Append(reader.ReadChar());
            }
        }

        return new MpmHeader
        {
            Unknown1 = unknown1,
            Unknown2 = unknown2,
            Unknown3 = unknown3,
            Width = width,
            Height = height,
            Unknown4 = unknown4,
            Unknown5 = unknown5,
            Unknown6 = unknown6,
            ColorPaletteFileIndex = colorPaletteFileIndex,
            ColorPaletteFileName = colorPaletteFile.ToString(),
            BitmapFileIndex = bitmapFileIndex,
            BitmapFileName = bitmapFile.ToString(),
            BgMapFileIndex = bgMapFileIndex,
            BgMapFileName = bgMapFile.ToString()
        };
    }
}
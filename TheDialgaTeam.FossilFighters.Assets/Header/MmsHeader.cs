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

public sealed class MmsHeader
{
    public const int FileHeader = 0x00534D4D;

    public int Unknown1 { get; init; }

    public int Unknown2 { get; init; }

    public int Unknown3 { get; init; }

    public int Unknown4 { get; init; }

    public int Unknown5 { get; init; }

    public int AnimationFileCount { get; init; }

    public int[] AnimationFileIndexes { get; init; } = null!;

    public string AnimationFileName { get; init; } = null!;

    public int ColorPaletteFileCount { get; init; }

    public int[] ColorPaletteFileIndexes { get; init; } = null!;

    public string ColorPaletteFileName { get; init; } = null!;

    public int BitmapFileCount { get; init; }

    public int[] BitmapFileIndexes { get; init; } = null!;

    public string BitmapFileName { get; init; } = null!;

    private MmsHeader()
    {
    }

    public static MmsHeader GetHeaderFromStream(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "MMS"));

        var unknown1 = reader.ReadInt32();
        var unknown2 = reader.ReadInt32();
        var unknown3 = reader.ReadInt32();
        var unknown4 = reader.ReadInt32();
        var unknown5 = reader.ReadInt32();

        var animationFileCount = reader.ReadInt32();
        var animationFileIndexes = new int[animationFileCount];
        var animationFileIndexOffset = reader.ReadInt32();
        var animationFileNameOffset = reader.ReadInt32();

        var colorPaletteFileCount = reader.ReadInt32();
        var colorPaletteFileIndexes = new int[colorPaletteFileCount];
        var colorPaletteFileIndexOffset = reader.ReadInt32();
        var colorPaletteFileNameOffset = reader.ReadInt32();

        var bitmapFileCount = reader.ReadInt32();
        var bitmapFileIndexes = new int[bitmapFileCount];
        var bitmapFileIndexOffset = reader.ReadInt32();
        var bitmapFileNameOffset = reader.ReadInt32();

        reader.BaseStream.Seek(animationFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < animationFileCount; i++)
        {
            animationFileIndexes[i] = reader.ReadInt32();
        }

        reader.BaseStream.Seek(animationFileNameOffset, SeekOrigin.Begin);

        var animationFileName = new StringBuilder();
        char animationFileNameChar;

        while ((animationFileNameChar = reader.ReadChar()) != '\0')
        {
            animationFileName.Append(animationFileNameChar);
        }

        reader.BaseStream.Seek(colorPaletteFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < colorPaletteFileCount; i++)
        {
            colorPaletteFileIndexes[i] = reader.ReadInt32();
        }

        reader.BaseStream.Seek(colorPaletteFileNameOffset, SeekOrigin.Begin);

        var colorPaletteFileName = new StringBuilder();
        char colorPaletteFileNameChar;

        while ((colorPaletteFileNameChar = reader.ReadChar()) != '\0')
        {
            colorPaletteFileName.Append(colorPaletteFileNameChar);
        }

        reader.BaseStream.Seek(bitmapFileIndexOffset, SeekOrigin.Begin);

        for (var i = 0; i < bitmapFileCount; i++)
        {
            bitmapFileIndexes[i] = reader.ReadInt32();
        }

        reader.BaseStream.Seek(bitmapFileNameOffset, SeekOrigin.Begin);

        var bitmapFileName = new StringBuilder();
        char bitmapFileNameChar;

        while ((bitmapFileNameChar = reader.ReadChar()) != '\0')
        {
            bitmapFileName.Append(bitmapFileNameChar);
        }

        return new MmsHeader
        {
            Unknown1 = unknown1,
            Unknown2 = unknown2,
            Unknown3 = unknown3,
            Unknown4 = unknown4,
            Unknown5 = unknown5,
            AnimationFileCount = animationFileCount,
            AnimationFileIndexes = animationFileIndexes,
            AnimationFileName = animationFileName.ToString(),
            ColorPaletteFileCount = colorPaletteFileCount,
            ColorPaletteFileIndexes = colorPaletteFileIndexes,
            ColorPaletteFileName = colorPaletteFileName.ToString(),
            BitmapFileCount = bitmapFileCount,
            BitmapFileIndexes = bitmapFileIndexes,
            BitmapFileName = bitmapFileName.ToString()
        };
    }
}
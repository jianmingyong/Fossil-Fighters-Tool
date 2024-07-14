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
using SixLabors.ImageSharp.PixelFormats;

namespace TheDialgaTeam.FossilFighters.Assets.Motion;

public enum ColorPaletteType
{
    Color16 = 0,
    Color256 = 1
}

public readonly struct ColorPalette(ColorPaletteType type, Bgra5551[] colors)
{
    public ColorPaletteType Type { get; } = type;

    public Bgra5551[] Colors { get; } = colors;

    public string ToJascPalString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("JASC-PAL");
        builder.AppendLine("0100");
        builder.AppendLine(Colors.Length.ToString());

        foreach (var color in Colors)
        {
            var rgba32 = new Rgba32();
            color.ToRgba32(ref rgba32);

            builder.AppendLine($"{rgba32.R} {rgba32.G} {rgba32.B}");
        }

        return builder.ToString();
    }
}
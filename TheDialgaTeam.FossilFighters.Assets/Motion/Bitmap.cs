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

using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Motion;

[PublicAPI]
public readonly struct Bitmap
{
    public int Width { get; }

    public int Height { get; }

    public ColorPaletteType ColorPaletteType { get; }

    public byte[] ColorPaletteIndexes { get; }

    public Bitmap(int width, int height, ColorPaletteType colorPaletteType, byte[] colorPaletteIndexes)
    {
        Width = width;
        Height = height;
        ColorPaletteType = colorPaletteType;
        ColorPaletteIndexes = colorPaletteIndexes;
    }
}
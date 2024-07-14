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

public static class MotionUtility
{
    public static ColorPalette GetColorPalette(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var paletteType = (ColorPaletteType) reader.ReadInt32();
        var colorTable = new Bgra5551[paletteType == ColorPaletteType.Color16 ? 16 : 256];

        for (var i = 0; i < colorTable.Length; i++)
        {
            var rawValue = reader.ReadUInt16();
            colorTable[i] = new Bgra5551((rawValue & 0x1F) / 31f, ((rawValue >> 5) & 0x1F) / 31f, ((rawValue >> 10) & 0x1F) / 31f, i == 0 ? 0 : 1);
        }

        return new ColorPalette(paletteType, colorTable);
    }

    public static Bitmap GetBitmap(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        var rawData = reader.ReadInt32();

        int width;
        int height;

        switch (rawData & 0xF)
        {
            case 0x00:
                width = 8;
                height = 8;
                break;

            case 0x01:
                width = 16;
                height = 8;
                break;

            case 0x02:
                width = 8;
                height = 16;
                break;

            case 0x04:
                width = 16;
                height = 16;
                break;

            case 0x05:
                width = 32;
                height = 8;
                break;

            case 0x06:
                width = 8;
                height = 32;
                break;

            case 0x08:
                width = 32;
                height = 32;
                break;

            case 0x09:
                width = 32;
                height = 16;
                break;

            case 0x0A:
                width = 16;
                height = 32;
                break;

            case 0x0C:
                width = 64;
                height = 64;
                break;

            case 0x0D:
                width = 64;
                height = 32;
                break;

            case 0x0E:
                width = 32;
                height = 64;
                break;

            default:
                throw new InvalidDataException();
        }

        var colorPaletteType = (ColorPaletteType) ((rawData >> 16) & 0x1);
        var colorPaletteIndexes = new List<byte>();

        while (stream.Position < stream.Length)
        {
            colorPaletteIndexes.Add(reader.ReadByte());
        }

        return new Bitmap(width, height, colorPaletteType, colorPaletteIndexes.ToArray());
    }

    public static SixLabors.ImageSharp.Image<Bgra5551> GetImage(ColorPalette colorPalette, Bitmap bitmap, int gridSize = 8)
    {
        var image = new SixLabors.ImageSharp.Image<Bgra5551>(bitmap.Width, bitmap.Height);

        var bitmapIndex = 0;
        var gridX = 0;
        var gridY = 0;

        if (bitmap.ColorPaletteType == ColorPaletteType.Color16)
        {
            while (bitmapIndex * 2 < bitmap.Width * bitmap.Height)
            {
                for (var y = 0; y < gridSize; y++)
                {
                    for (var x = 0; x < gridSize; x += 2)
                    {
                        image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[bitmap.ColorPaletteIndexes[bitmapIndex] & 0xF];
                        image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[bitmap.ColorPaletteIndexes[bitmapIndex] >> 4];

                        bitmapIndex++;
                    }
                }

                gridX++;

                if (gridX >= bitmap.Width / gridSize)
                {
                    gridX = 0;
                    gridY++;
                }
            }
        }
        else if (bitmap.ColorPaletteType == ColorPaletteType.Color256)
        {
            while (bitmapIndex < bitmap.Width * bitmap.Height)
            {
                for (var y = 0; y < gridSize; y++)
                {
                    for (var x = 0; x < gridSize; x++)
                    {
                        image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[bitmap.ColorPaletteIndexes[bitmapIndex++]];
                    }
                }

                gridX++;

                if (gridX >= bitmap.Width / gridSize)
                {
                    gridX = 0;
                    gridY++;
                }
            }
        }

        return image;
    }
}
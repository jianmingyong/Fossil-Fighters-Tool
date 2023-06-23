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
using TheDialgaTeam.FossilFighters.Assets.Header;

namespace TheDialgaTeam.FossilFighters.Assets.Image;

public static class ImageUtility
{
    private const int ColorPalette16FileSize = 16 * 2;
    private const int ColorPalette256FileSize = 256 * 2;

    public static ColorPalette GetColorPalette(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));

        using var tempStream = new MemoryStream();
        stream.CopyTo(tempStream);
        tempStream.Seek(0, SeekOrigin.Begin);

        using var reader = new BinaryReader(tempStream);
        
        var colorTable = new List<Bgra5551>();

        while (tempStream.Position < tempStream.Length)
        {
            var rawValue = reader.ReadUInt16();
            colorTable.Add(new Bgra5551((rawValue & 0x1F) / 31f, ((rawValue >> 5) & 0x1F) / 31f, ((rawValue >> 10) & 0x1F) / 31f, colorTable.Count == 0 ? 0 : 1));
        }
        
        return new ColorPalette(colorTable.Count <= 16 ? ColorPaletteType.Color16 : ColorPaletteType.Color256, colorTable.ToArray());
    }

    public static byte[] GetBitmap(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public static ChunkBitmap GetChunkBitmap(ColorPalette colorPalette, Stream bitmap, Stream bitmapIndex)
    {
        var result = new ChunkBitmap();

        using (var reader = new BinaryReader(bitmap, Encoding.UTF8, true))
        {
            while (bitmap.Position < bitmap.Length)
            {
                var chunk = new byte[colorPalette.Type == ColorPaletteType.Color16 ? 4 * 8 : 8 * 8];

                for (var i = 0; i < chunk.Length; i++)
                {
                    chunk[i] = reader.ReadByte();
                }

                result.ColorPaletteIndexes.Add(chunk);
            }
        }

        using (var reader = new BinaryReader(bitmapIndex, Encoding.UTF8, true))
        {
            while (bitmapIndex.Position < bitmapIndex.Length)
            {
                result.BitmapIndices.Add(reader.ReadUInt16());
            }
        }

        return result;
    }

    public static Image<Bgra5551> GetImage(MpmHeader header, ColorPalette colorPalette, byte[] bitmap, int gridSize = 8)
    {
        var image = new Image<Bgra5551>(header.Width, header.Height);
        var bitmapIndex = 0;

        if (colorPalette.Type == ColorPaletteType.Color16)
        {
            for (var y = 0; y < header.Height; y++)
            {
                for (var x = 0; x < header.Width; x += 2)
                {
                    image[x, y] = colorPalette.Colors[bitmap[bitmapIndex] >> 4];
                    image[x + 1, y] = colorPalette.Colors[bitmap[bitmapIndex] & 0xF];
                    bitmapIndex++;
                }
            }
        }
        else if (colorPalette.Type == ColorPaletteType.Color256)
        {
            for (var y = 0; y < header.Height; y++)
            {
                for (var x = 0; x < header.Width; x++)
                {
                    image[x, y] = colorPalette.Colors[bitmap[bitmapIndex]];
                    bitmapIndex++;
                }
            }
        }

        return image;
    }

    public static Image<Bgra5551> GetImage(MpmHeader header, ColorPalette colorPalette, ChunkBitmap chunkBitmap)
    {
        const int gridSize = 8;

        var image = new Image<Bgra5551>(header.Width, header.Height);
        var gridX = 0;
        var gridY = 0;

        foreach (var bitmapIndex in chunkBitmap.BitmapIndices)
        {
            if (colorPalette.Type == ColorPaletteType.Color16)
            {
                var horizontalFlip = (bitmapIndex & 0x400) > 0;
                var verticalFlip = (bitmapIndex & 0x800) > 0;

                if (!horizontalFlip && !verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x += 2)
                        {
                            var targetIndex = (x + y * gridSize) / 2;
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] >> 4];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] & 0xF];
                        }
                    }
                }
                else if (horizontalFlip && !verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x += 2)
                        {
                            var targetIndex = (gridSize - 1 - x + y * gridSize) / 2;
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] >> 4];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] & 0xF];
                        }
                    }
                }
                else if (!horizontalFlip && verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x += 2)
                        {
                            var targetIndex = (x + (gridSize - 1 - y) * gridSize) / 2;
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] & 0xF];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] >> 4];
                        }
                    }
                }
                else
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x += 2)
                        {
                            var targetIndex = (gridSize - 1 - x + (gridSize - 1 - y) * gridSize) / 2;
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] & 0xF];
                            image[x + 1 + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex] >> 4];
                        }
                    }
                }
            }
            else
            {
                var horizontalFlip = (bitmapIndex & 0x400) > 0;
                var verticalFlip = (bitmapIndex & 0x800) > 0;

                if (!horizontalFlip && !verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x++)
                        {
                            var targetIndex = x + y * gridSize;
                            var colorPaletteIndex = chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex];
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[colorPaletteIndex];
                        }
                    }
                }
                else if (horizontalFlip && !verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x++)
                        {
                            var targetIndex = gridSize - 1 - x + y * gridSize;
                            var colorPaletteIndex = chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex];
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[colorPaletteIndex];
                        }
                    }
                }
                else if (!horizontalFlip && verticalFlip)
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x++)
                        {
                            var targetIndex = x + (gridSize - 1 - y) * gridSize;
                            var colorPaletteIndex = chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex];
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[colorPaletteIndex];
                        }
                    }
                }
                else
                {
                    for (var y = 0; y < gridSize; y++)
                    {
                        for (var x = 0; x < gridSize; x++)
                        {
                            var targetIndex = gridSize - 1 - x + (gridSize - 1 - y) * gridSize;
                            var colorPaletteIndex = chunkBitmap.ColorPaletteIndexes[bitmapIndex & 0x3FF][targetIndex];
                            image[x + gridX * gridSize, y + gridY * gridSize] = colorPalette.Colors[colorPaletteIndex];
                        }
                    }
                }
            }

            gridX++;

            if (gridX >= header.Width / gridSize)
            {
                gridX = 0;
                gridY++;
            }
        }

        return image;
    }
}
using JetBrains.Annotations;
using SixLabors.ImageSharp.PixelFormats;

namespace TheDialgaTeam.FossilFighters.Assets.Image;

[PublicAPI]
public readonly struct ColorPalette
{
    public ColorPaletteType Type { get; }

    public Rgba32[] Table { get; }
    
    public ColorPalette(ColorPaletteType type, Rgba32[] table)
    {
        Type = type;
        Table = table;
    }
}
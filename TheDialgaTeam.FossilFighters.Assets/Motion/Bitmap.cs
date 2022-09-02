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
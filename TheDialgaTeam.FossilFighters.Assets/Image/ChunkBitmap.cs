using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Image;

[PublicAPI]
public class ChunkBitmap
{
    public List<byte[]> ColorPaletteIndexes { get; } = new();

    public List<int> BitmapIndices { get; } = new();
}
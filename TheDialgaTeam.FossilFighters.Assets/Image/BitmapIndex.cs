using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Image;

[PublicAPI]
public readonly struct BitmapIndex
{
    public byte BitmapChunkIndex { get; }
    
    public byte Unknown { get; }

    public BitmapIndex(byte bitmapChunkIndex, byte unknown)
    {
        BitmapChunkIndex = bitmapChunkIndex;
        Unknown = unknown;
    }
}
// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
// Copyright (C) 2023 Yong Jian Ming
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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TheDialgaTeam.FossilFighters.Assets.Utilities;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NdsFilesystem : IDisposable
{
    public char[] GameCode { get; }

    public FossilFightersGameType GameType { get; }

    public uint Arm9RomDataOffset { get; }

    public uint Arm9RomDataSize { get; }

    public uint Arm7RomDataOffset { get; }

    public uint Arm7RomDataSize { get; }

    public uint FileNameTableOffset { get; }

    public uint FileNameTableSize { get; }

    public uint FileAllocationTableOffset { get; }

    public uint FileAllocationTableSize { get; }

    public uint Arm9OverlayOffset { get; }

    public uint Arm9OverlaySize { get; }

    public uint Arm7OverlayOffset { get; }

    public uint Arm7OverlaySize { get; }

    public NitroRomDirectory RootDirectory { get; }

    internal MemoryStream Stream { get; }

    internal Dictionary<ushort, NitroRomDirectory> NitroRomDirectories { get; } = new();

    internal Dictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    internal Dictionary<ushort, NitroRomFile> OverlayFilesById { get; } = new();
    private const uint Arm9PostRomDataSize = 12;

    private static readonly Dictionary<ushort, uint> IconBannerSize = new()
    {
        { 0x0001, 0x0840 },
        { 0x0002, 0x0940 },
        { 0x0003, 0x1240 },
        { 0x0103, 0x23C0 }
    };

    private NdsFilesystem(MemoryStream stream)
    {
        Stream = stream;
        stream.Seek(0xC, SeekOrigin.Begin);

        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        GameCode = reader.ReadChars(4);

        GameType = GameCode.AsSpan() switch
        {
            "YKHE" => FossilFightersGameType.FF1English,
            "YKHJ" => FossilFightersGameType.FF1Japanese,
            "VDEE" => FossilFightersGameType.FFCEnglish,
            "VDEJ" => FossilFightersGameType.FFCJapanese,
            var _ => throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "FF1/FFC"))
        };

        stream.Seek(0x20, SeekOrigin.Begin);
        Arm9RomDataOffset = reader.ReadUInt32();

        stream.Seek(0x2C, SeekOrigin.Begin);
        Arm9RomDataSize = reader.ReadUInt32();

        stream.Seek(0x30, SeekOrigin.Begin);
        Arm7RomDataOffset = reader.ReadUInt32();

        stream.Seek(0x3C, SeekOrigin.Begin);
        Arm7RomDataSize = reader.ReadUInt32();

        stream.Seek(0x40, SeekOrigin.Begin);

        FileNameTableOffset = reader.ReadUInt32();
        FileNameTableSize = reader.ReadUInt32();

        FileAllocationTableOffset = reader.ReadUInt32();
        FileAllocationTableSize = reader.ReadUInt32();

        Arm9OverlayOffset = reader.ReadUInt32();
        Arm9OverlaySize = reader.ReadUInt32();

        Arm7OverlayOffset = reader.ReadUInt32();
        Arm7OverlaySize = reader.ReadUInt32();

        RootDirectory = new NitroRomDirectory(this, 0xF000, string.Empty);
    }

    public static NdsFilesystem FromStream(Stream stream)
    {
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return new NdsFilesystem(memoryStream);
    }

    public static async Task<NdsFilesystem> FromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return new NdsFilesystem(memoryStream);
    }

    private static void PatchHeader(Stream stream)
    {
        if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);

        // Update used rom space
        writer.Seek(0x80, SeekOrigin.Begin);
        writer.Write((uint) writer.BaseStream.Length);

        var checksum = GenerateCrc16(stream, 0, 0x15E);
        writer.Seek(0x15E, SeekOrigin.Begin);
        writer.Write(checksum);

        // Patch 0x180 (Remove dsi headers)
        writer.Seek(0x180, SeekOrigin.Begin);

        const int bufferSize = 0x4000 - 0x180;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            buffer.AsSpan(0, bufferSize).Clear();
            writer.Write(buffer, 0, bufferSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static ushort GenerateCrc16(Stream stream, int offset, int length)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        var buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        ushort result = 0xFFFF;

        for (var i = 0; i < length; i++)
        {
            result ^= buffer[i];

            for (var j = 0; j < 8; j++)
            {
                var carry = result & 1;
                result >>= 1;

                if (carry > 0)
                {
                    result ^= 0xA001;
                }
            }
        }

        return result;
    }

    public bool TryGetFileById(ushort id, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesById.TryGetValue(id, out nitroRomFile);
    }

    public bool TryGetFileByPath(string nitroRomFilePath, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesByPath.TryGetValue(nitroRomFilePath, out nitroRomFile);
    }

    [SuppressMessage("ReSharper.DPA", "DPA0003: Excessive memory allocations in LOH")]
    public async Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

        using var outputStream = new MemoryStream();
        using var reader = new BinaryReader(Stream, Encoding.ASCII);

        // Write the header first (we will update the header at the end eventually)
        await outputStream.WriteAsync(Stream.GetBuffer().AsMemory(0, 0x4000), cancellationToken).ConfigureAwait(false);

        // Write Arm9RomData
        await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) Arm9RomDataOffset, (int) (Arm9RomDataSize + Arm9PostRomDataSize)), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (Arm9OverlaySize > 0)
        {
            // Write Arm9OverlayTable
            await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) Arm9OverlayOffset, (int) Arm9OverlaySize), cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Write Arm9Overlay
            Stream.Seek(Arm9OverlayOffset, SeekOrigin.Begin);

            while (Stream.Position < Arm9OverlayOffset + Arm9OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = reader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    await nitroRomFileStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }

        // Write Arm7RomData
        await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) Arm7RomDataOffset, (int) Arm7RomDataSize), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (Arm7OverlaySize > 0)
        {
            // Write Arm7OverlayTable
            await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) Arm7OverlayOffset, (int) Arm7OverlaySize), cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

            // Write Arm7Overlay
            Stream.Seek(Arm7OverlayOffset, SeekOrigin.Begin);

            while (Stream.Position < Arm7OverlayOffset + Arm7OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = reader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    await nitroRomFileStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                }

                await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }

        // Write FNT
        await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) FileNameTableOffset, (int) FileNameTableSize), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Write FAT
        var newRomFileAllocationTableOffset = outputStream.Position;
        await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) FileAllocationTableOffset, (int) FileAllocationTableSize), cancellationToken).ConfigureAwait(false);
        await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Write Icon Stuff
        Stream.Seek(0x68, SeekOrigin.Begin);
        var iconOffset = reader.ReadUInt32();

        if (iconOffset != 0)
        {
            Stream.Seek(iconOffset, SeekOrigin.Begin);
            var iconSize = IconBannerSize[reader.ReadUInt16()];

            await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) iconOffset, (int) iconSize), cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Write Debug Rom
        Stream.Seek(0x160, SeekOrigin.Begin);
        var debugRomOffset = reader.ReadUInt32();

        if (debugRomOffset != 0)
        {
            var debugRomSize = reader.ReadUInt32();
            await outputStream.WriteAsync(Stream.GetBuffer().AsMemory((int) debugRomOffset, (int) debugRomSize), cancellationToken).ConfigureAwait(false);
            await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Write NitroRomFiles
        var offset = (uint) outputStream.Position;
        await using var writer = new BinaryWriter(outputStream, Encoding.ASCII, true);

        foreach (var nitroRomFile in NitroRomFilesById.Values.OrderBy(file => file.OriginalOffset))
        {
            var endOffset = offset + nitroRomFile.Size;

            outputStream.Seek(newRomFileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            writer.Write(offset);
            writer.Write(endOffset);

            outputStream.Seek(offset, SeekOrigin.Begin);

            using var file = nitroRomFile.OpenRead();
            await file.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);

            await outputStream.WriteAlignAsync(0x200, cancellationToken: cancellationToken).ConfigureAwait(false);

            offset = (uint) outputStream.Position;
        }

        PatchHeader(outputStream);

        outputStream.Seek(0, SeekOrigin.Begin);
        await outputStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        RootDirectory.Dispose();
        Stream.Dispose();
    }
}
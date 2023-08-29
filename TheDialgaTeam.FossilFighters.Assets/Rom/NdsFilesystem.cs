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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NdsFilesystem : IDisposable
{
    public char[] GameCode { get; }

    public FossilFightersGameType GameType { get; }

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

    internal SortedDictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    internal Dictionary<ushort, NitroRomFile> OverlayFilesById { get; } = new();

    private static Dictionary<ushort, uint> _iconBannerSize = new()
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

    public static NdsFilesystem FromFile(Stream stream)
    {
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return new NdsFilesystem(memoryStream);
    }

    public static async Task<NdsFilesystem> FromFileAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return new NdsFilesystem(memoryStream);
    }

    public bool TryGetFileById(ushort id, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesById.TryGetValue(id, out nitroRomFile);
    }

    public bool TryGetFileByPath(string nitroRomFilePath, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesByPath.TryGetValue(nitroRomFilePath, out nitroRomFile);
    }

    public void WriteTo(Stream stream)
    {
        using var newRomMemoryStream = new MemoryStream();
        using var newRomBinaryWriter = new BinaryWriter(newRomMemoryStream, Encoding.ASCII);
        using var binaryReader = new BinaryReader(Stream, Encoding.ASCII);
        
        // Write the header first (we will update the header at the end eventually)
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan(0, 0x4000));
        
        // Write Arm9RomData
        Stream.Seek(0x20, SeekOrigin.Begin);
        var arm9RomDataOffset = binaryReader.ReadUInt32();
        Stream.Seek(0x2C, SeekOrigin.Begin);
        var arm9RomDataSize = binaryReader.ReadUInt32() + 12;
        
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) arm9RomDataOffset, (int) arm9RomDataSize));
        Align(newRomBinaryWriter, 0x200);
        
        if (Arm9OverlaySize > 0)
        {
            // Write Arm9OverlayTable
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) Arm9OverlayOffset, (int) Arm9OverlaySize));
            Align(newRomBinaryWriter, 0x200);
            
            // Write Arm9Overlay
            Stream.Seek(Arm9OverlayOffset, SeekOrigin.Begin);
            
            while (Stream.Position < Arm9OverlayOffset + Arm9OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = binaryReader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    nitroRomFileStream.CopyTo(newRomMemoryStream);
                }
                
                Align(newRomBinaryWriter, 0x200);
                
                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }
        
        // Write Arm7RomData
        Stream.Seek(0x30, SeekOrigin.Begin);
        var arm7RomDataOffset = binaryReader.ReadUInt32();
        Stream.Seek(0x3C, SeekOrigin.Begin);
        var arm7RomDataSize = binaryReader.ReadUInt32();
        
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) arm7RomDataOffset, (int) arm7RomDataSize));
        Align(newRomBinaryWriter, 0x200);
        
        if (Arm7OverlaySize > 0)
        {
            // Write Arm7OverlayTable
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) Arm7OverlayOffset, (int) Arm7OverlaySize));
            Align(newRomBinaryWriter, 0x200);
            
            // Write Arm7Overlay
            Stream.Seek(Arm7OverlayOffset, SeekOrigin.Begin);
            
            while (Stream.Position < Arm7OverlayOffset + Arm7OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = binaryReader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    nitroRomFileStream.CopyTo(newRomMemoryStream);
                }
                
                Align(newRomBinaryWriter, 0x200);
                
                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }
        
        // Write FNT
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) FileNameTableOffset, (int) FileNameTableSize));
        Align(newRomBinaryWriter, 0x200);
        
        // Write FAT
        var newRomFileAllocationTableOffset = newRomMemoryStream.Position;
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) FileAllocationTableOffset, (int) FileAllocationTableSize));
        Align(newRomBinaryWriter, 0x200);
        
        // Write Icon Stuff
        Stream.Seek(0x68, SeekOrigin.Begin);
        var iconOffset = binaryReader.ReadUInt32();

        if (iconOffset != 0)
        {
            Stream.Seek(iconOffset, SeekOrigin.Begin);
            var iconSize = _iconBannerSize[binaryReader.ReadUInt16()];
        
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) iconOffset, (int) iconSize));
            Align(newRomBinaryWriter, 0x200);
        }
        
        // Write Debug Rom
        Stream.Seek(0x160, SeekOrigin.Begin);
        var debugRomOffset = binaryReader.ReadUInt32();

        if (debugRomOffset != 0)
        {
            var debugRomSize = binaryReader.ReadUInt32();
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) debugRomOffset, (int) debugRomSize));
            Align(newRomBinaryWriter, 0x200);
        }
        
        // Write NitroRomFiles
        var offset = (uint) newRomMemoryStream.Position;
        var lastFileId = NitroRomFilesById.Values.Last().Id;

        foreach (var nitroRomFile in NitroRomFilesById.Values)
        {
            var endOffset = offset + nitroRomFile.Size;

            newRomMemoryStream.Seek(newRomFileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            newRomBinaryWriter.Write(offset);
            newRomBinaryWriter.Write(endOffset);

            newRomMemoryStream.Seek(offset, SeekOrigin.Begin);

            using var file = nitroRomFile.OpenRead();
            file.CopyTo(newRomMemoryStream);

            if (nitroRomFile.Id != lastFileId)
            {
                Align(newRomBinaryWriter, 0x200);
            }

            offset = (uint) newRomMemoryStream.Position;
        }
        
        // Patch Unitcode (Only NDS mode)
        newRomMemoryStream.Seek(0x12, SeekOrigin.Begin);
        newRomBinaryWriter.Write((byte) 0);
        
        // Patch 0x1C - 0x1D (NDS Region)
        newRomMemoryStream.Seek(0x1C, SeekOrigin.Begin);
        newRomBinaryWriter.Write((byte) 0);
        newRomBinaryWriter.Write((byte) 0);
        
        // Update used rom space
        newRomMemoryStream.Seek(0x80, SeekOrigin.Begin);
        newRomBinaryWriter.Write((uint) newRomMemoryStream.Length);
        
        // TODO: Fix Header Checksum 0x15E
        
        // Patch 0x180 (Remove dsi headers)
        newRomMemoryStream.Seek(0x180, SeekOrigin.Begin);

        const int bufferSize = 0x4000 - 0x180;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            buffer.AsSpan(0, bufferSize).Clear();
            newRomBinaryWriter.Write(buffer, 0, bufferSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        newRomMemoryStream.Seek(0, SeekOrigin.Begin);
        newRomMemoryStream.CopyTo(stream);
        return;

        void Align(BinaryWriter binaryWriter, long length, byte value = 0xFF)
        {
            var remainder = binaryWriter.BaseStream.Position % length;
            if (remainder == 0) return;
            
            var paddingRequired = length - remainder;
            var paddingBuffer = ArrayPool<byte>.Shared.Rent((int) paddingRequired);

            try
            {
                paddingBuffer.AsSpan(0, (int) paddingRequired).Fill(value);
                binaryWriter.Write(paddingBuffer, 0, (int) paddingRequired);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(paddingBuffer);
            }
        }
    }

    public async Task WriteToAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var newRomMemoryStream = new MemoryStream();
        await using var newRomBinaryWriter = new BinaryWriter(newRomMemoryStream, Encoding.ASCII);
        using var binaryReader = new BinaryReader(Stream, Encoding.ASCII);
        
        // Write the header first (we will update the header at the end eventually)
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan(0, 0x4000));
        
        // Write Arm9RomData
        Stream.Seek(0x20, SeekOrigin.Begin);
        var arm9RomDataOffset = binaryReader.ReadUInt32();
        Stream.Seek(0x2C, SeekOrigin.Begin);
        var arm9RomDataSize = binaryReader.ReadUInt32() + 12;
        
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) arm9RomDataOffset, (int) arm9RomDataSize));
        Align(newRomBinaryWriter, 0x200);
        
        if (Arm9OverlaySize > 0)
        {
            // Write Arm9OverlayTable
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) Arm9OverlayOffset, (int) Arm9OverlaySize));
            Align(newRomBinaryWriter, 0x200);
            
            // Write Arm9Overlay
            Stream.Seek(Arm9OverlayOffset, SeekOrigin.Begin);
            
            while (Stream.Position < Arm9OverlayOffset + Arm9OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = binaryReader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    await nitroRomFileStream.CopyToAsync(newRomMemoryStream, cancellationToken).ConfigureAwait(false);
                }
                
                Align(newRomBinaryWriter, 0x200);
                
                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }
        
        // Write Arm7RomData
        Stream.Seek(0x30, SeekOrigin.Begin);
        var arm7RomDataOffset = binaryReader.ReadUInt32();
        Stream.Seek(0x3C, SeekOrigin.Begin);
        var arm7RomDataSize = binaryReader.ReadUInt32();
        
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) arm7RomDataOffset, (int) arm7RomDataSize));
        Align(newRomBinaryWriter, 0x200);
        
        if (Arm7OverlaySize > 0)
        {
            // Write Arm7OverlayTable
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) Arm7OverlayOffset, (int) Arm7OverlaySize));
            Align(newRomBinaryWriter, 0x200);
            
            // Write Arm7Overlay
            Stream.Seek(Arm7OverlayOffset, SeekOrigin.Begin);
            
            while (Stream.Position < Arm7OverlayOffset + Arm7OverlaySize)
            {
                var tempPosition = Stream.Position;
                Stream.Seek(0x18, SeekOrigin.Current);
                var fileId = binaryReader.ReadUInt32();

                if (OverlayFilesById.TryGetValue((ushort) fileId, out var nitroRomFile))
                {
                    using var nitroRomFileStream = nitroRomFile.OpenRead();
                    await nitroRomFileStream.CopyToAsync(newRomMemoryStream, cancellationToken).ConfigureAwait(false);
                }
                
                Align(newRomBinaryWriter, 0x200);
                
                Stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
            }
        }
        
        Debug.Assert(newRomMemoryStream.Position == FileNameTableOffset);
        
        // Write FNT
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) FileNameTableOffset, (int) FileNameTableSize));
        Align(newRomBinaryWriter, 0x200);
        
        // Write FAT
        var newRomFileAllocationTableOffset = newRomMemoryStream.Position;
        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) FileAllocationTableOffset, (int) FileAllocationTableSize));
        Align(newRomBinaryWriter, 0x200);
        
        // Write Icon Stuff
        Stream.Seek(0x68, SeekOrigin.Begin);
        var iconOffset = binaryReader.ReadUInt32();

        if (iconOffset != 0)
        {
            Stream.Seek(iconOffset, SeekOrigin.Begin);
            var iconSize = _iconBannerSize[binaryReader.ReadUInt16()];
        
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) iconOffset, (int) iconSize));
            Align(newRomBinaryWriter, 0x200);
        }
        
        // Write Debug Rom
        Stream.Seek(0x160, SeekOrigin.Begin);
        var debugRomOffset = binaryReader.ReadUInt32();

        if (debugRomOffset != 0)
        {
            var debugRomSize = binaryReader.ReadUInt32();
            newRomMemoryStream.Write(Stream.GetBuffer().AsSpan((int) debugRomOffset, (int) debugRomSize));
            Align(newRomBinaryWriter, 0x200);
        }
        
        // Write NitroRomFiles
        var offset = (uint) newRomMemoryStream.Position;
        var lastFileId = NitroRomFilesById.Values.Last().Id;

        foreach (var nitroRomFile in NitroRomFilesById.Values)
        {
            var endOffset = offset + nitroRomFile.Size;

            newRomMemoryStream.Seek(newRomFileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            newRomBinaryWriter.Write(offset);
            newRomBinaryWriter.Write(endOffset);

            newRomMemoryStream.Seek(offset, SeekOrigin.Begin);

            using var file = nitroRomFile.OpenRead();
            await file.CopyToAsync(newRomMemoryStream, cancellationToken).ConfigureAwait(false);

            if (nitroRomFile.Id != lastFileId)
            {
                Align(newRomBinaryWriter, 0x200);
            }

            offset = (uint) newRomMemoryStream.Position;
        }
        
        // Patch Unitcode (Only NDS mode)
        newRomMemoryStream.Seek(0x12, SeekOrigin.Begin);
        newRomBinaryWriter.Write((byte) 0);
        
        // Patch 0x1C - 0x1D (NDS Region)
        newRomMemoryStream.Seek(0x1C, SeekOrigin.Begin);
        newRomBinaryWriter.Write((byte) 0);
        newRomBinaryWriter.Write((byte) 0);
        
        // Update used rom space
        newRomMemoryStream.Seek(0x80, SeekOrigin.Begin);
        newRomBinaryWriter.Write((uint) newRomMemoryStream.Length);
        
        // TODO: Fix Header Checksum 0x15E
        
        // Patch 0x180 (Remove dsi headers)
        newRomMemoryStream.Seek(0x180, SeekOrigin.Begin);

        const int bufferSize = 0x4000 - 0x180;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            buffer.AsSpan(0, bufferSize).Clear();
            newRomBinaryWriter.Write(buffer, 0, bufferSize);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        
        newRomMemoryStream.Seek(0, SeekOrigin.Begin);
        await newRomMemoryStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        return;

        void Align(BinaryWriter binaryWriter, long length, byte value = 0xFF)
        {
            var remainder = binaryWriter.BaseStream.Position % length;
            if (remainder == 0) return;
            
            var paddingRequired = length - remainder;
            var paddingBuffer = ArrayPool<byte>.Shared.Rent((int) paddingRequired);

            try
            {
                paddingBuffer.AsSpan(0, (int) paddingRequired).Fill(value);
                binaryWriter.Write(paddingBuffer, 0, (int) paddingRequired);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(paddingBuffer);
            }
        }
    }

    public void Dispose()
    {
        RootDirectory.Dispose();
        Stream.Dispose();
    }
}
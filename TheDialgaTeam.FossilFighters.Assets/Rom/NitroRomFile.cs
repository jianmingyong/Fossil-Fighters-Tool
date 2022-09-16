﻿// Fossil Fighters Tool is used to decompress and compress MAR archives used in Fossil Fighters game.
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
using TheDialgaTeam.FossilFighters.Assets.Archive;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

[PublicAPI]
public sealed class NitroRomFile : INitroRom
{
    public NitroRomDirectory Directory { get; }

    public string FullPath => $"{Directory.FullPath}/{Name}";

    public ushort Id { get; }

    public string Name { get; }

    public NitroRomType FileType { get; }

    public int Size => (int) (IsDirty ? NitroRomData.Length : OriginalSize);

    public int OriginalOffset { get; }

    public int OriginalSize { get; }

    internal MemoryStream NitroRomData { get; } = new();

    internal bool IsDirty { get; private set; }

    private readonly NdsFilesystem _ndsFilesystem;

    private bool _isLoaded;

    public NitroRomFile(NdsFilesystem ndsFilesystem, NitroRomDirectory directory, ushort id, string name)
    {
        _ndsFilesystem = ndsFilesystem;

        Directory = directory;

        Id = id;
        Name = name;

        var stream = ndsFilesystem.BaseStream;
        var reader = ndsFilesystem.Reader;

        stream.Seek(ndsFilesystem.FileAllocationTableOffset + id * 8, SeekOrigin.Begin);

        OriginalOffset = reader.ReadInt32();
        OriginalSize = reader.ReadInt32() - OriginalOffset;

        stream.Seek(OriginalOffset, SeekOrigin.Begin);

        var fileHeader = reader.ReadInt32();

        FileType = MarArchive.HeaderId == fileHeader ? NitroRomType.MarArchive : NitroRomType.File;

        ndsFilesystem.NitroRomFilesById.Add(id, this);
        ndsFilesystem.NitroRomFilesByPath.Add(FullPath, this);
    }

    public MemoryStream OpenRead()
    {
        if (!_isLoaded && !IsDirty)
        {
            _ndsFilesystem.BaseStream.Seek(OriginalOffset, SeekOrigin.Begin);

            NitroRomData.Seek(0, SeekOrigin.Begin);
            NitroRomData.SetLength(0);

            NitroRomData.Write(_ndsFilesystem.Reader.ReadBytes(OriginalSize));

            _isLoaded = true;
        }

        return new MemoryStream(NitroRomData.GetBuffer(), 0, (int) NitroRomData.Length, false);
    }

    public void WriteFrom(byte[] buffer, int offset, int count)
    {
        BeforeWrite();
        NitroRomData.Write(buffer, offset, count);
    }

    public void WriteFrom(ReadOnlySpan<byte> buffer)
    {
        BeforeWrite();
        NitroRomData.Write(buffer);
    }

    public void WriteFrom(Stream stream)
    {
        BeforeWrite();
        stream.CopyTo(NitroRomData);
    }

    public Task WriteFromAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        BeforeWrite();
        return NitroRomData.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public ValueTask WriteFromAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BeforeWrite();
        return NitroRomData.WriteAsync(buffer, cancellationToken);
    }

    public Task WriteFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        BeforeWrite();
        return stream.CopyToAsync(NitroRomData, cancellationToken);
    }

    private void BeforeWrite()
    {
        IsDirty = true;
        NitroRomData.Seek(0, SeekOrigin.Begin);
        NitroRomData.SetLength(0);
    }

    internal void Dispose()
    {
        NitroRomData.Dispose();
    }
}
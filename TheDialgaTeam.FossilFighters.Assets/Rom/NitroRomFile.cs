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

using System.Diagnostics;
using System.Text;
using TheDialgaTeam.FossilFighters.Assets.Archive;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NitroRomFile : INitroRom
{
    public NitroRomDirectory Directory { get; }

    public string FullPath => $"{Directory.FullPath}/{Name}";

    public ushort Id { get; }

    public string Name { get; }

    public uint Size => (uint) (_nitroRomData?.Length ?? OriginalSize);

    public uint OriginalOffset { get; }

    public uint OriginalSize { get; }

    public NitroRomType FileType { get; private set; }

    private readonly NdsFilesystem _ndsFilesystem;
    private MemoryStream? _nitroRomData;

    public NitroRomFile(NdsFilesystem ndsFilesystem, NitroRomDirectory directory, ushort id, string name)
    {
        _ndsFilesystem = ndsFilesystem;

        Directory = directory;

        Id = id;
        Name = name;

        var stream = ndsFilesystem.Stream;
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        stream.Seek(ndsFilesystem.FileAllocationTableOffset + id * 8, SeekOrigin.Begin);

        OriginalOffset = reader.ReadUInt32();
        OriginalSize = reader.ReadUInt32() - OriginalOffset;

        stream.Seek(OriginalOffset, SeekOrigin.Begin);

        var fileHeader = reader.ReadUInt32();
        FileType = MarArchive.HeaderId == fileHeader ? NitroRomType.MarArchive : NitroRomType.File;

        ndsFilesystem.NitroRomFilesById.Add(id, this);
        ndsFilesystem.NitroRomFilesByPath.Add(FullPath, this);
    }

    public MemoryStream OpenRead()
    {
        return _nitroRomData is not null ? new MemoryStream(_nitroRomData.GetBuffer(), 0, (int) _nitroRomData.Length, false, true) : new MemoryStream(_ndsFilesystem.Stream.GetBuffer(), (int) OriginalOffset, (int) OriginalSize, false);
    }

    public void WriteFrom(byte[] buffer, int offset, int count)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            _nitroRomData.Write(buffer, offset, count);
        }
        finally
        {
            AfterWrite();
        }
    }

    public void WriteFrom(ReadOnlySpan<byte> buffer)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            _nitroRomData.Write(buffer);
        }
        finally
        {
            AfterWrite();
        }
    }

    public void WriteFrom(Stream stream)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            stream.CopyTo(_nitroRomData);
        }
        finally
        {
            AfterWrite();
        }
    }

    public Task WriteFromAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            return _nitroRomData.WriteAsync(buffer, offset, count, cancellationToken);
        }
        finally
        {
            AfterWrite();
        }
    }

    public ValueTask WriteFromAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            return _nitroRomData.WriteAsync(buffer, cancellationToken);
        }
        finally
        {
            AfterWrite();
        }
    }

    public Task WriteFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        BeforeWrite();

        try
        {
            Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");
            return stream.CopyToAsync(_nitroRomData, cancellationToken);
        }
        finally
        {
            AfterWrite();
        }
    }

    private void BeforeWrite()
    {
        _nitroRomData ??= new MemoryStream();
        _nitroRomData.Seek(0, SeekOrigin.Begin);
        _nitroRomData.SetLength(0);
    }

    private void AfterWrite()
    {
        Debug.Assert(_nitroRomData != null, nameof(_nitroRomData) + " != null");

        _nitroRomData.Seek(0, SeekOrigin.Begin);

        using var reader = new BinaryReader(_nitroRomData, Encoding.ASCII, true);
        var fileHeader = reader.ReadUInt32();

        FileType = MarArchive.HeaderId == fileHeader ? NitroRomType.MarArchive : NitroRomType.File;
    }

    internal void Dispose()
    {
        _nitroRomData?.Dispose();
    }
}
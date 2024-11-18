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

using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Platform.Storage;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TheDialgaTeam.FossilFighters.Assets;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.Rom;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Models;

public sealed class NitroRomNode : ReactiveObject
{
    public ObservableCollection<NitroRomNode> ChildNodes { get; } = [];

    public string Name => _nitroRom.Name;

    [Reactive]
    public NitroRomType FileType { get; private set; }

    [Reactive]
    public string FileTypeDisplay { get; private set; } = string.Empty;

    [Reactive]
    public uint Size { get; private set; }

    [Reactive]
    public bool IsDirty { get; private set; }

    private const string FileFolderTypeName = "File Folder";
    private const string FileTypeName = "File";
    private const string MarArchiveTypeName = "Mar Archive";

    private readonly INitroRom _nitroRom;

    public NitroRomNode(INitroRom nitroRom)
    {
        _nitroRom = nitroRom;
        Update();

        if (nitroRom is not NitroRomDirectory nitroRomDirectory) return;

        foreach (var subDirectory in nitroRomDirectory.SubDirectories)
        {
            ChildNodes.Add(new NitroRomNode(subDirectory));
        }

        foreach (var file in nitroRomDirectory.Files)
        {
            ChildNodes.Add(new NitroRomNode(file));
        }
    }

    public MemoryStream OpenRead()
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        return nitroRomFile.OpenRead();
    }

    public void WriteFrom(byte[] buffer, int offset, int count)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        nitroRomFile.WriteFrom(buffer, offset, count);
        Update();
    }

    public void WriteFrom(ReadOnlySpan<byte> buffer)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        nitroRomFile.WriteFrom(buffer);
        Update();
    }

    public void WriteFrom(Stream stream)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        nitroRomFile.WriteFrom(stream);
        Update();
    }

    public async Task WriteFromAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        await nitroRomFile.WriteFromAsync(buffer, offset, count, cancellationToken);
        Update();
    }

    public async ValueTask WriteFromAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        await nitroRomFile.WriteFromAsync(buffer, cancellationToken);
        Update();
    }

    public async Task WriteFromAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (_nitroRom is not NitroRomFile nitroRomFile) throw new InvalidOperationException();
        await nitroRomFile.WriteFromAsync(stream, cancellationToken);
        Update();
    }

    public async Task ExportFilesAsync(IStorageFolder storageFolder)
    {
        if (FileType is NitroRomType.File or NitroRomType.MarArchive)
        {
            using var targetFileStream = OpenRead();

            var outputFile = await storageFolder.CreateFileAsync(Name);
            if (outputFile is null) return;

            await using var outputFileStream = await outputFile.OpenWriteAsync();
            await targetFileStream.CopyToAsync(outputFileStream);
        }
        else
        {
            foreach (var childNode in ChildNodes)
            {
                var targetFolder = await storageFolder.CreateFolderAsync(Name);
                if (targetFolder is null) throw new IOException();
                await childNode.ExportFilesAsync(targetFolder);
            }
        }
    }

    public async Task DecompressFilesAsync(IStorageFolder storageFolder)
    {
        switch (FileType)
        {
            case NitroRomType.File:
            {
                using var inputStream = OpenRead();

                var outputFile = await storageFolder.CreateFileAsync(Name);
                if (outputFile is null) return;

                await using var outputFileStream = await outputFile.OpenWriteAsync();
                await inputStream.CopyToAsync(outputFileStream);
                break;
            }

            case NitroRomType.MarArchive:
            {
                var targetOutputFolder = await storageFolder.CreateFolderAsync(Name);
                if (targetOutputFolder is null) throw new IOException();

                using var inputStream = OpenRead();
                using var marArchive = new MarArchive(inputStream);

                var metadata = new Dictionary<int, McmFileMetadata>();

                for (var index = 0; index < marArchive.Entries.Count; index++)
                {
                    var outputFile = await targetOutputFolder.CreateFileAsync($"{index}.bin");
                    if (outputFile is null) throw new IOException();

                    var marArchiveEntry = marArchive.Entries[index];
                    await using var mcmFile = marArchiveEntry.OpenRead();
                    await using var outputFileStream = await outputFile.OpenWriteAsync();
                    await mcmFile.CopyToAsync(outputFileStream);

                    metadata.Add(index, mcmFile.GetFileMetadata());
                }

                var outputMetaFile = await targetOutputFolder.CreateFileAsync("meta.json");
                if (outputMetaFile is null) throw new IOException();

                await using var outputMetaFileStream = await outputMetaFile.OpenWriteAsync();
                await JsonSerializer.SerializeAsync(outputMetaFileStream, metadata, CustomJsonSerializerContext.Custom.DictionaryInt32McmFileMetadata);
                break;
            }

            case NitroRomType.FileFolder:
            {
                foreach (var childNode in ChildNodes)
                {
                    var targetFolder = await storageFolder.CreateFolderAsync(Name);
                    if (targetFolder is null) throw new IOException();
                    await childNode.DecompressFilesAsync(targetFolder);
                }

                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Update()
    {
        FileType = _nitroRom.FileType;

        FileTypeDisplay = _nitroRom.FileType switch
        {
            NitroRomType.FileFolder => FileFolderTypeName,
            NitroRomType.File => FileTypeName,
            NitroRomType.MarArchive => MarArchiveTypeName,
            NitroRomType.Overlay => FileTypeName,
            var _ => throw new ArgumentOutOfRangeException(nameof(_nitroRom.FileType))
        };

        if (_nitroRom is NitroRomFile nitroRomFile)
        {
            Size = nitroRomFile.Size;
            IsDirty = nitroRomFile.IsDirty;
        }
    }
}
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

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NitroRomDirectory : INitroRom
{
    public string FullPath
    {
        get
        {
            if (_fullPath is not null) return _fullPath;

            var temp = new List<string> { Name };
            var currentDirectory = this;

            while (currentDirectory._parentDirectory is not null)
            {
                temp.Add(currentDirectory._parentDirectory.Name);
                currentDirectory = currentDirectory._parentDirectory;
            }

            _fullPath = string.Join("/", temp.AsEnumerable().Reverse());
            return _fullPath;
        }
    }

    public string Name { get; }

    public NitroRomType FileType => NitroRomType.FileFolder;

    public List<NitroRomDirectory> SubDirectories { get; } = [];

    public List<NitroRomFile> Files { get; } = [];
    private readonly NitroRomDirectory? _parentDirectory;

    private string? _fullPath;

    public NitroRomDirectory(NdsFilesystem ndsFilesystem, ushort id, string name)
    {
        ndsFilesystem.NitroRomDirectories.Add(id, this);

        Name = name;

        var stream = ndsFilesystem.Stream;
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        stream.Seek(ndsFilesystem.FileNameTableOffset + (id & 0xFFF) * 8, SeekOrigin.Begin);

        var subTableOffset = reader.ReadUInt32();
        var firstFileId = reader.ReadUInt16();
        var parentDirectory = reader.ReadUInt16();

        if (id != 0xF000)
        {
            _parentDirectory = ndsFilesystem.NitroRomDirectories[parentDirectory];
        }
        else
        {
            // For root directory, let add overlay into the selection as well.
            if (ndsFilesystem.Arm9OverlaySize != 0)
            {
                stream.Seek(ndsFilesystem.Arm9OverlayOffset, SeekOrigin.Begin);

                while (stream.Position < ndsFilesystem.Arm9OverlayOffset + ndsFilesystem.Arm9OverlaySize)
                {
                    var tempPosition = stream.Position;
                    var overlayName = reader.ReadUInt32();
                    stream.Seek(0x14, SeekOrigin.Current);
                    var fileId = reader.ReadUInt32();

                    Files.Add(new NitroRomFile(ndsFilesystem, this, (ushort) fileId, $"overlay9_{overlayName}", NitroRomType.Overlay));
                    stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
                }
            }

            if (ndsFilesystem.Arm7OverlaySize != 0)
            {
                stream.Seek(ndsFilesystem.Arm7OverlayOffset, SeekOrigin.Begin);

                while (stream.Position < ndsFilesystem.Arm7OverlayOffset + ndsFilesystem.Arm7OverlaySize)
                {
                    var tempPosition = stream.Position;
                    var overlayName = reader.ReadUInt32();
                    stream.Seek(0x14, SeekOrigin.Current);
                    var fileId = reader.ReadUInt32();

                    Files.Add(new NitroRomFile(ndsFilesystem, this, (ushort) fileId, $"overlay7_{overlayName}", NitroRomType.Overlay));
                    stream.Seek(tempPosition + 0x20, SeekOrigin.Begin);
                }
            }
        }

        stream.Seek(ndsFilesystem.FileNameTableOffset + subTableOffset, SeekOrigin.Begin);

        byte subTableType;

        while ((subTableType = reader.ReadByte()) != 0)
        {
            switch (subTableType)
            {
                case > 0x80:
                {
                    // Directories
                    var directoryName = reader.ReadChars(subTableType - 0x80).AsSpan().ToString();
                    var subDirectoryId = reader.ReadUInt16();

                    var tempPosition = stream.Position;
                    SubDirectories.Add(new NitroRomDirectory(ndsFilesystem, subDirectoryId, directoryName));
                    stream.Seek(tempPosition, SeekOrigin.Begin);
                    break;
                }

                case < 0x80:
                {
                    // Files
                    var fileName = reader.ReadChars(subTableType).AsSpan().ToString();

                    var tempPosition = stream.Position;
                    Files.Add(new NitroRomFile(ndsFilesystem, this, firstFileId, fileName));
                    stream.Seek(tempPosition, SeekOrigin.Begin);

                    firstFileId++;
                    break;
                }
            }
        }
    }

    internal void Dispose()
    {
        foreach (var nitroRomFile in Files)
        {
            nitroRomFile.Dispose();
        }

        foreach (var nitroRomDirectory in SubDirectories)
        {
            nitroRomDirectory.Dispose();
        }
    }
}
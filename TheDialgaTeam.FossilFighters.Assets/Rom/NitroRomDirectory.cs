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

using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

[PublicAPI]
public class NitroRomDirectory : INitroRom, IDisposable
{
    public string FullPath
    {
        get
        {
            if (_fullPath == null)
            {
                var temp = new List<string> { Name };
                var currentDirectory = this;

                while (currentDirectory._parentDirectory != null)
                {
                    temp.Add(currentDirectory._parentDirectory.Name);
                    currentDirectory = currentDirectory._parentDirectory;
                }

                temp.Reverse();

                _fullPath = string.Join("/", temp);
            }

            return _fullPath;
        }
    }

    public string Name { get; }

    public string FileType => "File Folder";

    public List<NitroRomDirectory> SubDirectories { get; } = new();

    public List<NitroRomFile> Files { get; } = new();

    private string? _fullPath;
    private NitroRomDirectory? _parentDirectory;

    public NitroRomDirectory(NdsFilesystem ndsFilesystem, ushort id, string name)
    {
        Name = name;
        ndsFilesystem.NitroRomDirectories.Add(id, this);

        var stream = ndsFilesystem.BaseStream;
        stream.Seek(ndsFilesystem.FileNameTableOffset + (id & 0xFFF) * 8, SeekOrigin.Begin);

        var reader = ndsFilesystem.Reader;

        var subTableOffset = reader.ReadUInt32();
        var firstFileId = reader.ReadUInt16();
        var parentDirectory = reader.ReadUInt16();

        if (id != 0xF000)
        {
            _parentDirectory = ndsFilesystem.NitroRomDirectories[parentDirectory];
        }

        stream.Seek(ndsFilesystem.FileNameTableOffset + subTableOffset, SeekOrigin.Begin);

        byte subTableType;

        while ((subTableType = reader.ReadByte()) != 0)
        {
            if (subTableType > 0x80)
            {
                // Directories
                var length = subTableType - 0x80;
                var directoryName = reader.ReadChars(length).AsSpan().ToString();
                var subDirectoryId = reader.ReadUInt16();

                var tempPosition = stream.Position;
                SubDirectories.Add(new NitroRomDirectory(ndsFilesystem, subDirectoryId, directoryName));
                stream.Seek(tempPosition, SeekOrigin.Begin);
            }
            else if (subTableType < 0x80)
            {
                // Files
                var length = subTableType;
                var fileName = reader.ReadChars(length).AsSpan().ToString();

                var tempPosition = stream.Position;
                Files.Add(new NitroRomFile(ndsFilesystem, this, firstFileId, fileName));
                stream.Seek(tempPosition, SeekOrigin.Begin);

                firstFileId++;
            }
        }
    }

    public void Dispose()
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
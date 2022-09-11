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
using TheDialgaTeam.FossilFighters.Assets.Rom;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Models;

public class NitroRomNode
{
    public ObservableCollection<NitroRomNode> Subfolders { get; } = new();

    public string FullPath => _nitroRom.FullPath;

    public string Name => _nitroRom.Name;

    public string FileType => _nitroRom.FileType;

    public bool IsFile => _nitroRom is NitroRomFile;

    public long Size => _nitroRom is NitroRomFile test ? test.Size : 0;

    private readonly INitroRom _nitroRom;

    public NitroRomNode(INitroRom nitroRom)
    {
        _nitroRom = nitroRom;

        if (nitroRom is not NitroRomDirectory nitroRomDirectory) return;

        foreach (var subDirectory in nitroRomDirectory.SubDirectories)
        {
            Subfolders.Add(new NitroRomNode(subDirectory));
        }

        foreach (var file in nitroRomDirectory.Files)
        {
            Subfolders.Add(new NitroRomNode(file));
        }
    }
}
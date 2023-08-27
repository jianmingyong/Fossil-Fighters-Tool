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

using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using TheDialgaTeam.FossilFighters.Assets.Rom;
using TheDialgaTeam.FossilFighters.Tool.Gui.Models;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.ViewModels.Editor;

public sealed class FFCTextEditorViewModel : ReactiveObject
{
    public List<DtxDataFile> DtxDataFiles { get; set; } = new();

    [Reactive]
    public DtxDataFile SelectedDtxDataFile { get; set; }

    private readonly NdsFilesystem _loadedRom;

    public FFCTextEditorViewModel()
    {
    }

    public FFCTextEditorViewModel(NdsFilesystem loadedRom)
    {
        _loadedRom = loadedRom;

        var directory = loadedRom.RootDirectory.SubDirectories.Single(directory => directory.Name.Equals("text"));

        foreach (var file in directory.Files)
        {
            DtxDataFiles.Add(new DtxDataFile(file));
        }

        SelectedDtxDataFile = DtxDataFiles[0];
    }
}
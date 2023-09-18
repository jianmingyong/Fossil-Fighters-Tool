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

using System.IO;
using TheDialgaTeam.FossilFighters.Assets.Archive;
using TheDialgaTeam.FossilFighters.Assets.GameData;
using TheDialgaTeam.FossilFighters.Assets.Rom;

namespace TheDialgaTeam.FossilFighters.Tool.Gui.Models;

public sealed class DtxDataFile
{
    public string DisplayName { get; }

    private readonly NitroRomFile _nitroRomFile;
    private DtxFile? _dtxFile;

    public DtxDataFile(NitroRomFile nitroRomFile)
    {
        _nitroRomFile = nitroRomFile;

        DisplayName = nitroRomFile.Name switch
        {
            var _ => nitroRomFile.Name
        };
    }

    public void LoadData()
    {
        using var marArchive = new MarArchive(_nitroRomFile.OpenRead());
        using var mcmFileStream = marArchive.Entries[0].OpenRead();
        using var rawDataStream = new MemoryStream();
        mcmFileStream.CopyTo(rawDataStream);

        _dtxFile = DtxFile.ReadFromRawStream(rawDataStream);
    }
}
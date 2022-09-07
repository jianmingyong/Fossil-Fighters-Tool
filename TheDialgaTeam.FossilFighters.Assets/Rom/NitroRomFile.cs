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

using System.Buffers;
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

[PublicAPI]
public class NitroRomFile : INitroRom
{
    public ushort Id { get; }

    public string FullPath { get; }

    public string Name { get; }

    public uint Offset { get; }

    public uint Size { get; }

    private readonly NdsFilesystem _ndsFilesystem;

    public NitroRomFile(NdsFilesystem ndsFilesystem, ushort id, string name, string directory)
    {
        _ndsFilesystem = ndsFilesystem;

        Id = id;
        Name = name;
        FullPath = $"{directory}/{name}";

        var stream = ndsFilesystem.BaseStream;
        stream.Seek(ndsFilesystem.FileAllocationTableOffset + id * 8, SeekOrigin.Begin);

        Offset = ndsFilesystem.Reader.ReadUInt32();
        Size = ndsFilesystem.Reader.ReadUInt32() - Offset;

        ndsFilesystem.NitroRomFilesById.Add(id, this);
        ndsFilesystem.NitroRomFilesByPath.Add(FullPath, this);
    }

    public MemoryStream Open()
    {
        var stream = new MemoryStream((int) Size);
        _ndsFilesystem.BaseStream.Seek(Offset, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        long fileRemaining = Size;

        try
        {
            while (fileRemaining > 0)
            {
                int bytesRead;
                if ((bytesRead = _ndsFilesystem.Reader.Read(buffer, 0, 4096)) == 0) throw new EndOfStreamException();

                stream.Write(buffer, 0, (int) Math.Min(bytesRead, fileRemaining));
                fileRemaining -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}
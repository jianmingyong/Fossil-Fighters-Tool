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
using TheDialgaTeam.FossilFighters.Assets.Archive;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

[PublicAPI]
public class NitroRomFile : INitroRom, IDisposable
{
    public NitroRomDirectory Directory { get; }

    public string FullPath => $"{Directory.FullPath}/{Name}";

    public ushort Id { get; }

    public string Name { get; }

    public string FileType { get; }

    public long Size => _fileCacheMemoryStream?.Length ?? OriginalSize;

    public long OriginalOffset { get; }

    public long OriginalSize { get; }

    private readonly NdsFilesystem _ndsFilesystem;
    private MemoryStream? _fileCacheMemoryStream;

    public NitroRomFile(NdsFilesystem ndsFilesystem, NitroRomDirectory directory, ushort id, string name)
    {
        _ndsFilesystem = ndsFilesystem;

        Directory = directory;

        Id = id;
        Name = name;

        var stream = ndsFilesystem.BaseStream;
        var reader = ndsFilesystem.Reader;
        
        stream.Seek(ndsFilesystem.FileAllocationTableOffset + id * 8, SeekOrigin.Begin);

        OriginalOffset = reader.ReadUInt32();
        OriginalSize = reader.ReadUInt32() - OriginalOffset;

        stream.Seek(OriginalOffset, SeekOrigin.Begin);

        var fileHeader = reader.ReadInt32();

        FileType = MarArchive.HeaderId == fileHeader ? "MAR Archive" : "File";

        ndsFilesystem.NitroRomFilesById.Add(id, this);
        ndsFilesystem.NitroRomFilesByPath.Add(FullPath, this);
    }

    public MemoryStream Open()
    {
        if (_fileCacheMemoryStream != null)
        {
            _fileCacheMemoryStream.Seek(0, SeekOrigin.Begin);
            return _fileCacheMemoryStream;
        }

        _fileCacheMemoryStream = new MemoryStream((int) OriginalSize);
        _ndsFilesystem.BaseStream.Seek(OriginalOffset, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        var fileRemaining = OriginalSize;

        try
        {
            while (fileRemaining > 0)
            {
                int bytesRead;
                if ((bytesRead = _ndsFilesystem.Reader.Read(buffer, 0, 4096)) == 0) throw new EndOfStreamException();

                _fileCacheMemoryStream.Write(buffer, 0, (int) Math.Min(bytesRead, fileRemaining));
                fileRemaining -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        _fileCacheMemoryStream.Seek(0, SeekOrigin.Begin);
        return _fileCacheMemoryStream;
    }

    public void Dispose()
    {
        _fileCacheMemoryStream?.Dispose();
    }
}
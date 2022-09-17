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
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

[PublicAPI]
public sealed class NdsFilesystem : IDisposable
{
    // ReSharper disable InconsistentNaming
    public const string FF1EnglishGameCode = "YKHE";
    public const string FF1JapaneseGameCode = "YKHJ";
    public const string FFCEnglishGameCode = "VDEE";
    public const string FFCJapaneseGameCode = "VDEJ";
    // ReSharper restore InconsistentNaming
    
    public Stream BaseStream { get; }
    
    public string GameCode { get; }

    public uint FileNameTableOffset { get; }

    public uint FileNameTableSize { get; }

    public uint FileAllocationTableOffset { get; }

    public uint FileAllocationTableSize { get; }

    public NitroRomDirectory RootDirectory { get; }

    internal BinaryReader Reader { get; }

    internal Dictionary<ushort, NitroRomDirectory> NitroRomDirectories { get; } = new();

    internal SortedDictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    private NdsFilesystem(FileStream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));

        BaseStream = stream;
        
        Reader = new BinaryReader(stream, Encoding.UTF8);
        stream.Seek(0x0C, SeekOrigin.Begin);
        
        GameCode = Reader.ReadChars(4).AsSpan().TrimEnd('\0').ToString();

        if (!GameCode.Equals(FF1EnglishGameCode) && !GameCode.Equals(FF1JapaneseGameCode) &&
            !GameCode.Equals(FFCEnglishGameCode) && !GameCode.Equals(FFCJapaneseGameCode))
        {
            throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "FF1/FFC"));
        }

        stream.Seek(0x40, SeekOrigin.Begin);

        FileNameTableOffset = Reader.ReadUInt32();
        FileNameTableSize = Reader.ReadUInt32();

        FileAllocationTableOffset = Reader.ReadUInt32();
        FileAllocationTableSize = Reader.ReadUInt32();

        RootDirectory = new NitroRomDirectory(this, 0xF000, string.Empty);
    }

    public static NdsFilesystem FromFile(string filePath)
    {
        return new NdsFilesystem(new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite));
    }

    public NitroRomFile GetFileById(ushort id)
    {
        return NitroRomFilesById[id];
    }

    public NitroRomFile GetFileByPath(string filePath)
    {
        return NitroRomFilesByPath[filePath];
    }

    public IEnumerable<NitroRomFile> EnumerateFiles()
    {
        return NitroRomFilesByPath.Values;
    }

    public void SaveChanges()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Reader.Dispose();
        RootDirectory.Dispose();
    }
}
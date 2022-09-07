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
    public Stream BaseStream { get; }

    public string GameTitle { get; }

    public string GameCode { get; }

    public string MakerCode { get; }

    public uint FileNameTableOffset { get; }

    public uint FileNameTableSize { get; }

    public uint FileAllocationTableOffset { get; }

    public uint FileAllocationTableSize { get; }

    public NitroRomDirectory RootDirectory { get; }

    internal BinaryReader Reader { get; }

    //internal BinaryWriter Writer { get; }

    internal Dictionary<ushort, NitroRomDirectory> NitroRomDirectories { get; } = new();

    internal Dictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    private NdsFilesystem(FileStream stream, bool leaveOpen = false)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        //if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

        BaseStream = stream;

        Reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen);
        //Writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);

        var gameTitleBuilder = new StringBuilder();
        char tempChar;

        while ((tempChar = Reader.ReadChar()) != '\0')
        {
            gameTitleBuilder.Append(tempChar);
        }

        GameTitle = gameTitleBuilder.ToString();

        stream.Seek(0x0C, SeekOrigin.Begin);

        GameCode = Reader.ReadChars(4).AsSpan().ToString();
        MakerCode = Reader.ReadChars(2).AsSpan().ToString();

        if (!GameTitle.Contains("KASEKI")) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "FF1/FFC"));

        stream.Seek(0x40, SeekOrigin.Begin);

        FileNameTableOffset = Reader.ReadUInt32();
        FileNameTableSize = Reader.ReadUInt32();

        FileAllocationTableOffset = Reader.ReadUInt32();
        FileAllocationTableSize = Reader.ReadUInt32();

        RootDirectory = new NitroRomDirectory(this, 0xF000, string.Empty);
    }

    public static NdsFilesystem FromFile(string filePath)
    {
        return new NdsFilesystem(File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public MemoryStream GetFileById(ushort id)
    {
        return NitroRomFilesById[id].Open();
    }

    public MemoryStream GetFileByPath(string filePath)
    {
        return NitroRomFilesByPath[filePath].Open();
    }

    public IEnumerable<string> EnumerateFiles()
    {
        foreach (var key in NitroRomFilesByPath.Keys)
        {
            yield return key;
        }
    }

    public void Dispose()
    {
        Reader.Dispose();
        //Writer.Dispose();
    }
}
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

using System.Buffers;
using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NdsFilesystem : IDisposable
{
    public const string FF1EnglishGameCode = "YKHE";
    public const string FF1JapaneseGameCode = "YKHJ";
    public const string FFCEnglishGameCode = "VDEE";
    public const string FFCJapaneseGameCode = "VDEJ";

    public char[] GameCode { get; }

    public uint FileNameTableOffset { get; }

    public uint FileNameTableSize { get; }

    public uint FileAllocationTableOffset { get; }

    public uint FileAllocationTableSize { get; }

    public NitroRomDirectory RootDirectory { get; }

    internal BinaryReader Reader { get; }

    internal Dictionary<ushort, NitroRomDirectory> NitroRomDirectories { get; } = new();

    internal SortedDictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    private NdsFilesystem(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));

        Reader = new BinaryReader(stream, Encoding.ASCII);

        stream.Seek(0xC, SeekOrigin.Begin);

        GameCode = Reader.ReadChars(4);

        var gameCode = GameCode.AsSpan();

        if (!gameCode.SequenceEqual(FF1EnglishGameCode) && !gameCode.SequenceEqual(FF1JapaneseGameCode) &&
            !gameCode.SequenceEqual(FFCEnglishGameCode) && !gameCode.SequenceEqual(FFCJapaneseGameCode))
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
        return new NdsFilesystem(new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None));
    }

    public NitroRomFile GetFileById(ushort id)
    {
        return NitroRomFilesById[id];
    }

    public NitroRomFile GetFileByPath(string filePath)
    {
        return NitroRomFilesByPath[filePath];
    }

    public void SaveChanges(string targetFile)
    {
        using var newRomMemoryStream = new MemoryStream();
        using var binaryReader = new BinaryReader(newRomMemoryStream, Encoding.ASCII);
        using var binaryWriter = new BinaryWriter(newRomMemoryStream, Encoding.ASCII);

        var lastFileId = NitroRomFilesById.Last().Value.Id;
        var offset = NitroRomFilesById.First().Value.OriginalOffset;

        long remainingSize = offset;
        var bufferSize = (int) Math.Min(4096, remainingSize);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        Reader.BaseStream.Seek(0, SeekOrigin.Begin);

        try
        {
            while (remainingSize > 0)
            {
                int read;
                if ((read = Reader.BaseStream.Read(buffer, 0, bufferSize)) == 0) throw new EndOfStreamException();

                newRomMemoryStream.Write(buffer, 0, (int) Math.Min(remainingSize, read));
                remainingSize -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        foreach (var nitroRomFile in NitroRomFilesById.Values)
        {
            var endOffset = offset + nitroRomFile.Size;

            newRomMemoryStream.Seek(FileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            binaryWriter.Write(offset);
            binaryWriter.Write(endOffset);

            newRomMemoryStream.Seek(offset, SeekOrigin.Begin);

            using var file = nitroRomFile.OpenRead();
            file.CopyTo(newRomMemoryStream);

            if (nitroRomFile.Id != lastFileId && file.Length % 0x200 > 0)
            {
                var paddingRequired = 0x200 - file.Length % 0x200;
                var paddingBuffer = ArrayPool<byte>.Shared.Rent((int) paddingRequired);

                try
                {
                    paddingBuffer.AsSpan(0, (int) paddingRequired).Fill(0xFF);
                    newRomMemoryStream.Write(paddingBuffer, 0, (int) paddingRequired);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(paddingBuffer);
                }
            }

            if (endOffset % 0x200 > 0)
            {
                offset = endOffset + 0x200 - endOffset % 0x200;
            }
            else
            {
                offset = endOffset;
            }
        }

        using var targetFileStream = File.OpenWrite(targetFile);
        newRomMemoryStream.Seek(0, SeekOrigin.Begin);
        newRomMemoryStream.CopyTo(targetFileStream);
    }

    public void Dispose()
    {
        Reader.Dispose();
        RootDirectory.Dispose();
    }
}
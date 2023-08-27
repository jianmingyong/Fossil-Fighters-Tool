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
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.Rom;

public sealed class NdsFilesystem : IDisposable
{
    public char[] GameCode { get; }

    public FossilFightersGameType GameType { get; }

    public uint FileNameTableOffset { get; }

    public uint FileNameTableSize { get; }

    public uint FileAllocationTableOffset { get; }

    public uint FileAllocationTableSize { get; }

    public NitroRomDirectory RootDirectory { get; }

    internal MemoryStream Stream { get; }

    internal Dictionary<ushort, NitroRomDirectory> NitroRomDirectories { get; } = new();

    internal SortedDictionary<ushort, NitroRomFile> NitroRomFilesById { get; } = new();

    internal Dictionary<string, NitroRomFile> NitroRomFilesByPath { get; } = new();

    private NdsFilesystem(MemoryStream stream)
    {
        Stream = stream;
        stream.Seek(0xC, SeekOrigin.Begin);

        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        GameCode = reader.ReadChars(4);

        GameType = GameCode.AsSpan() switch
        {
            FF1EnglishGameCode => FossilFightersGameType.FF1English,
            FF1JapaneseGameCode => FossilFightersGameType.FF1Japanese,
            FFCEnglishGameCode => FossilFightersGameType.FFCEnglish,
            FFCJapaneseGameCode => FossilFightersGameType.FFCJapanese,
            var _ => throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "FF1/FFC"))
        };

        stream.Seek(0x40, SeekOrigin.Begin);

        FileNameTableOffset = reader.ReadUInt32();
        FileNameTableSize = reader.ReadUInt32();

        FileAllocationTableOffset = reader.ReadUInt32();
        FileAllocationTableSize = reader.ReadUInt32();

        RootDirectory = new NitroRomDirectory(this, 0xF000, string.Empty);
    }

    public static NdsFilesystem FromFile(string filePath)
    {
        using var fileStream = File.OpenRead(filePath);
        var memoryStream = new MemoryStream();
        fileStream.CopyTo(memoryStream);
        return new NdsFilesystem(memoryStream);
    }

    public static NdsFilesystem FromFile(Stream stream)
    {
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return new NdsFilesystem(memoryStream);
    }

    public static async Task<NdsFilesystem> FromFileAsync(string filePath)
    {
        await using var fileStream = File.OpenRead(filePath);
        var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return new NdsFilesystem(memoryStream);
    }

    public static async Task<NdsFilesystem> FromFileAsync(Stream stream)
    {
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
        return new NdsFilesystem(memoryStream);
    }

    public bool TryGetFileById(ushort id, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesById.TryGetValue(id, out nitroRomFile);
    }

    public bool TryGetFileByPath(string nitroRomFilePath, [MaybeNullWhen(false)] out NitroRomFile nitroRomFile)
    {
        return NitroRomFilesByPath.TryGetValue(nitroRomFilePath, out nitroRomFile);
    }

    public void WriteTo(Stream stream)
    {
        using var newRomMemoryStream = new MemoryStream();
        using var newRomBinaryWriter = new BinaryWriter(newRomMemoryStream, Encoding.ASCII);

        var offset = NitroRomFilesById.Values.First().OriginalOffset;
        var lastFileId = NitroRomFilesById.Values.Last().Id;

        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan(0, (int) offset));

        foreach (var nitroRomFile in NitroRomFilesById.Values)
        {
            var endOffset = offset + nitroRomFile.Size;

            newRomMemoryStream.Seek(FileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            newRomBinaryWriter.Write(offset);
            newRomBinaryWriter.Write(endOffset);

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

        newRomMemoryStream.Seek(0, SeekOrigin.Begin);
        newRomMemoryStream.CopyTo(stream);
    }

    public async Task WriteToAsync(Stream stream)
    {
        using var newRomMemoryStream = new MemoryStream();
        await using var newRomBinaryWriter = new BinaryWriter(newRomMemoryStream, Encoding.ASCII);

        var offset = NitroRomFilesById.Values.First().OriginalOffset;
        var lastFileId = NitroRomFilesById.Values.Last().Id;

        newRomMemoryStream.Write(Stream.GetBuffer().AsSpan(0, (int) offset));

        foreach (var nitroRomFile in NitroRomFilesById.Values)
        {
            var endOffset = offset + nitroRomFile.Size;

            newRomMemoryStream.Seek(FileAllocationTableOffset + nitroRomFile.Id * 8, SeekOrigin.Begin);
            newRomBinaryWriter.Write(offset);
            newRomBinaryWriter.Write(endOffset);

            newRomMemoryStream.Seek(offset, SeekOrigin.Begin);

            using var file = nitroRomFile.OpenRead();
            await file.CopyToAsync(newRomMemoryStream).ConfigureAwait(false);

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

        newRomMemoryStream.Seek(0, SeekOrigin.Begin);
        await newRomMemoryStream.CopyToAsync(stream).ConfigureAwait(false);
    }

    public void Dispose()
    {
        RootDirectory.Dispose();
        Stream.Dispose();
    }

    // ReSharper disable InconsistentNaming
    private const string FF1EnglishGameCode = "YKHE";
    private const string FF1JapaneseGameCode = "YKHJ";
    private const string FFCEnglishGameCode = "VDEE";

    private const string FFCJapaneseGameCode = "VDEJ";
    // ReSharper restore InconsistentNaming
}
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

using System.Text;

namespace TheDialgaTeam.FossilFighters.Assets.GameData;

public sealed class DtxFile
{
    public const uint FileHeader = 0x00585444;

    public string[] Texts { get; set; } = Array.Empty<string>();

    public static DtxFile ReadFromStream(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadUInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DTX"));

        var textCount = reader.ReadUInt32();
        var textPointerAddress = reader.ReadUInt32();
        var textDataAddresses = new uint[textCount];

        reader.BaseStream.Seek(textPointerAddress, SeekOrigin.Begin);

        for (var i = 0; i < textCount; i++)
        {
            textDataAddresses[i] = reader.ReadUInt32();
        }

        var textBuilder = new StringBuilder();
        var texts = new string[textCount];

        for (var i = 0; i < textCount; i++)
        {
            stream.Seek(textDataAddresses[i], SeekOrigin.Begin);

            char nextChar;

            while ((nextChar = reader.ReadChar()) != '\0')
            {
                textBuilder.Append(nextChar);
            }

            texts[i] = textBuilder.ToString();
            textBuilder.Clear();
        }

        return new DtxFile { Texts = texts };
    }

    public void WriteToStream(Stream stream)
    {
        if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(FileHeader);
        writer.Write((uint) Texts.Length);
        writer.Write((uint) 0xC);

        var textDataAddress = (uint) (0xC + Texts.Length * 4);

        foreach (var text in Texts)
        {
            writer.Write(textDataAddress);
            textDataAddress += (uint) text.Length + 1;
        }

        foreach (var text in Texts)
        {
            writer.Write(text.AsSpan());
            writer.Write((byte) 0);
        }

        while (writer.BaseStream.Length % 4 != 0)
        {
            writer.Write((byte) 0);
        }
    }
}
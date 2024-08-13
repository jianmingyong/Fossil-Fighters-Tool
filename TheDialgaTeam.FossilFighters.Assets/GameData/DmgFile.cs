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
using System.Text.Json;
using TheDialgaTeam.FossilFighters.Assets.Utilities;

namespace TheDialgaTeam.FossilFighters.Assets.GameData;

public sealed record DmgMessage(uint MessageId, uint Unknown, string Message);

public sealed class DmgFile
{
    public const uint FileHeader = 0x00474D44;

    public DmgMessage[] Messages { get; set; } = [];

    public static DmgFile ReadFromRawStream(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException(Localization.StreamIsNotSeekable, nameof(stream));

        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        if (reader.ReadUInt32() != FileHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotFormat, "DMG"));

        var messagePointerCount = reader.ReadUInt32();
        var messagePointerAddress = reader.ReadUInt32();
        var messageDataAddresses = new uint[messagePointerCount];

        stream.Seek(messagePointerAddress, SeekOrigin.Begin);

        for (var i = 0; i < messagePointerCount; i++)
        {
            messageDataAddresses[i] = reader.ReadUInt32();
        }

        var stringBuilder = new StringBuilder();
        var messages = new DmgMessage[messagePointerCount];

        for (var i = 0; i < messagePointerCount; i++)
        {
            stream.Seek(messageDataAddresses[i], SeekOrigin.Begin);
            messages[i] = new DmgMessage(reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadNullTerminatedString(stringBuilder));
            stringBuilder.Clear();
        }

        return new DmgFile { Messages = messages };
    }

    public static DmgFile ReadFromJsonStream(Stream stream)
    {
        if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
        return JsonSerializer.Deserialize(stream, CustomJsonSerializerContext.Custom.DmgFile) ?? new DmgFile();
    }
    
    public void WriteToStream(Stream stream)
    {
        if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(FileHeader);
        writer.Write((uint) Messages.Length);
        writer.Write((uint) 0xC);

        var textDataAddress = (uint) (0xC + Messages.Length * 4);

        foreach (var message in Messages)
        {
            writer.Write(textDataAddress);
            textDataAddress += (uint) Encoding.UTF8.GetByteCount(message.Message) + 1 + 4 + 4;
        }

        foreach (var message in Messages)
        {
            writer.Write(message.MessageId);
            writer.Write(message.Unknown);
            writer.Write(message.Message.AsSpan());
            writer.Write((byte) 0);
        }

        while (stream.Length % 4 != 0)
        {
            writer.Write((byte) 0);
        }
    }
}
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
using System.Text;
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

[PublicAPI]
public class MarArchiveEntry : IDisposable
{
    internal readonly MemoryStream MemoryStream;

    public MarArchiveEntry()
    {
        MemoryStream = new MemoryStream();
    }

    public MarArchiveEntry(BinaryReader reader, int fileOffset, int fileLength)
    {
        if (reader.BaseStream is MemoryStream memoryStream)
        {
            MemoryStream = new MemoryStream(memoryStream.GetBuffer(), fileOffset, fileLength);
        }
        else
        {
            MemoryStream = new MemoryStream(Math.Max(0, fileLength));
            reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var fileRemaining = fileLength;

            try
            {
                while (fileRemaining > 0)
                {
                    var read = reader.Read(buffer, 0, 4096);
                    if (read == 0) throw new EndOfStreamException();

                    MemoryStream.Write(buffer, 0, read > fileRemaining ? fileRemaining : read);
                    fileRemaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public McmFileStream OpenRead()
    {
        MemoryStream.Seek(0, SeekOrigin.Begin);
        return new McmFileStream(MemoryStream, CompressibleStreamMode.Decompress, true);
    }

    public McmFileStream OpenWrite()
    {
        MemoryStream.Seek(0, SeekOrigin.Begin);
        MemoryStream.SetLength(0);
        return new McmFileStream(MemoryStream, CompressibleStreamMode.Compress, true);
    }

    internal int GetDecompressedDataSize()
    {
        using var reader = new BinaryReader(MemoryStream, Encoding.UTF8, true);
        MemoryStream.Seek(4, SeekOrigin.Begin);
        return reader.ReadInt32();
    }

    public void Dispose()
    {
        MemoryStream.Dispose();
    }
}
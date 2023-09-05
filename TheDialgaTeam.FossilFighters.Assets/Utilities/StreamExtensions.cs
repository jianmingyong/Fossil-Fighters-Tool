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

namespace TheDialgaTeam.FossilFighters.Assets.Utilities;

public static class StreamExtensions
{
    public static void WriteAlign(this Stream stream, int length, byte value = 0xFF)
    {
        var remainder = (int) (stream.Position % length);
        if (remainder == 0) return;

        var paddingRequired = length - remainder;
        var paddingBuffer = ArrayPool<byte>.Shared.Rent(paddingRequired);

        try
        {
            paddingBuffer.AsSpan(0, paddingRequired).Fill(value);
            stream.Write(paddingBuffer, 0, paddingRequired);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(paddingBuffer);
        }
    }

    public static async ValueTask WriteAlignAsync(this Stream stream, int length, byte value = 0xFF, CancellationToken cancellationToken = default)
    {
        var remainder = (int) (stream.Position % length);
        if (remainder == 0) return;

        var paddingRequired = length - remainder;
        var paddingBuffer = ArrayPool<byte>.Shared.Rent(paddingRequired);

        try
        {
            paddingBuffer.AsSpan(0, paddingRequired).Fill(value);
            await stream.WriteAsync(paddingBuffer.AsMemory(0, paddingRequired), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(paddingBuffer);
        }
    }
}
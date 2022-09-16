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

using System.Diagnostics;
using System.Text;
using JetBrains.Annotations;

namespace TheDialgaTeam.FossilFighters.Assets.Archive;

[PublicAPI]
public sealed class MarArchive : IDisposable
{
    public const int HeaderId = 0x0052414D;

    public Stream BaseStream { get; }

    public MarArchiveMode Mode { get; }

    public IReadOnlyList<MarArchiveEntry> Entries
    {
        get
        {
            ReadEntries();
            return _entries;
        }
    }

    private readonly BinaryReader? _reader;
    private readonly BinaryWriter? _writer;

    private readonly List<MarArchiveEntry> _entries = new();

    private bool _hasEntriesRead;

    public MarArchive(Stream stream, MarArchiveMode mode = MarArchiveMode.Read, bool leaveOpen = false)
    {
        BaseStream = stream;
        Mode = mode;

        Stream? inputStream = null;

        switch (mode)
        {
            case MarArchiveMode.Read:
                if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));

                if (stream.CanSeek)
                {
                    inputStream = stream;

                    _reader = new BinaryReader(inputStream, Encoding.UTF8, leaveOpen);
                }
                else
                {
                    try
                    {
                        inputStream = new MemoryStream((int) Math.Max(0, stream.Length));
                        stream.CopyTo(inputStream);
                        if (!leaveOpen) stream.Dispose();
                    }
                    catch (Exception)
                    {
                        inputStream?.Dispose();
                        throw;
                    }

                    _reader = new BinaryReader(inputStream);
                }

                break;

            case MarArchiveMode.Update:
                if (!stream.CanRead) throw new ArgumentException(Localization.StreamIsNotReadable, nameof(stream));
                if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

                if (stream.CanSeek)
                {
                    inputStream = stream;

                    _reader = new BinaryReader(inputStream, Encoding.UTF8, leaveOpen);
                    _writer = new BinaryWriter(inputStream, Encoding.UTF8, leaveOpen);
                }
                else
                {
                    try
                    {
                        inputStream = new MemoryStream((int) Math.Max(0, stream.Length));
                        stream.CopyTo(inputStream);
                    }
                    catch (Exception)
                    {
                        inputStream?.Dispose();
                        throw;
                    }

                    _reader = new BinaryReader(inputStream);
                    _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);
                }

                break;

            case MarArchiveMode.Create:
                if (!stream.CanWrite) throw new ArgumentException(Localization.StreamIsNotWriteable, nameof(stream));

                _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);
                _hasEntriesRead = true;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    public MarArchiveEntry CreateEntry()
    {
        var temp = new MarArchiveEntry();
        _entries.Add(temp);
        return temp;
    }

    public MarArchiveEntry CreateOrUpdateEntry(int fileIndex)
    {
        if (fileIndex < _entries.Count) return _entries[fileIndex];

        var temp = new MarArchiveEntry();
        _entries.Add(temp);
        return temp;
    }

    public MarArchiveEntry InsertEntry(int fileIndex)
    {
        var temp = new MarArchiveEntry();
        _entries.Insert(fileIndex, temp);
        return temp;
    }

    public void DeleteEntry(int fileIndex)
    {
        _entries.RemoveAt(fileIndex);
    }

    private void Flush()
    {
        if (Mode == MarArchiveMode.Read) return;

        Debug.Assert(_writer != null, nameof(_writer) + " != null");

        if (_writer.BaseStream.CanSeek)
        {
            _writer.BaseStream.Seek(0, SeekOrigin.Begin);
            _writer.BaseStream.SetLength(0);
        }

        _writer.Write(HeaderId);
        _writer.Write(_entries.Count);

        var startingIndex = 0x08 + 8 * _entries.Count;

        foreach (var entry in _entries)
        {
            _writer.Write(startingIndex);
            _writer.Write(entry.GetDecompressedDataSize());
            startingIndex += (int) entry.MemoryStream.Length;
        }

        foreach (var entry in _entries)
        {
            entry.MemoryStream.WriteTo(_writer.BaseStream);
        }
    }

    private void ReadEntries()
    {
        if (_hasEntriesRead) return;
        _hasEntriesRead = true;

        Debug.Assert(_reader != null, nameof(_reader) + " != null");

        var header = _reader.ReadInt32();
        if (header != HeaderId) throw new InvalidDataException(string.Format(Localization.StreamIsNotArchive, "MAR"));

        var numberOfFiles = _reader.ReadInt32();
        var offsets = new List<(int offset, int dataSize)>(numberOfFiles);

        for (var i = 0; i < numberOfFiles; i++)
        {
            offsets.Add((_reader.ReadInt32(), _reader.ReadInt32()));
        }

        for (var i = 0; i < numberOfFiles; i++)
        {
            var endingOffset = i + 1 < numberOfFiles ? offsets[i + 1].offset : _reader.BaseStream.Length;
            _entries.Add(new MarArchiveEntry(_reader, offsets[i].offset, (int) (endingOffset - offsets[i].offset)));
        }
    }

    public void Dispose()
    {
        Flush();

        _reader?.Dispose();
        _writer?.Dispose();

        foreach (var entry in _entries)
        {
            entry.Dispose();
        }
    }
}
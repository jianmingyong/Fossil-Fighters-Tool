using System.Buffers;
using System.Text;

namespace Fossil_Fighters_Tool;

public class McmFileReaderOld : IDisposable
{
    public readonly struct McmFilePointer
    {
        public int DataOffset { get; init; }
    }

    private const int McmFileHeaderId = 0x004D434D;

    public int DataTotalSize { get; }

    public int Unknown2 { get; }

    public int Unknown3 { get; }
    
    public McmFilePointer[] FilePointers { get; }
    
    public int EndOffset { get; }

    private readonly FileStream _stream;

    public McmFileReaderOld(FileStream stream)
    {
        _stream = stream;

        using var binaryReader = new BinaryReader(stream, Encoding.ASCII, true);

        if (binaryReader.ReadInt32() != McmFileHeaderId)
        {
            throw new Exception("This is not a MCM file.");
        }

        DataTotalSize = binaryReader.ReadInt32();
        Unknown2 = binaryReader.ReadInt32();

        var filePointerCount = binaryReader.ReadInt32();
        FilePointers = new McmFilePointer[filePointerCount];

        Unknown3 = binaryReader.ReadInt32();

        for (var i = 0; i < filePointerCount; i++)
        {
            FilePointers[i] = new McmFilePointer
            {
                DataOffset = binaryReader.ReadInt32()
            };
        }

        EndOffset = binaryReader.ReadInt32();
    }

    public void ExtractTo(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(4096);
        var totalWritten = 0;

        try
        {
            for (var i = 0; i < FilePointers.Length; i++)
            {
                var filePointer = FilePointers[i];
                var nextPointer = i + 1 < FilePointers.Length ? FilePointers[i + 1] : new McmFilePointer { DataOffset = EndOffset };

                _stream.Seek(filePointer.DataOffset, SeekOrigin.Begin);

                using var fileStream = new FileStream(Path.Combine(outputDirectory, $"{i}.bin"), FileMode.Create);

                var fileLength = nextPointer.DataOffset - filePointer.DataOffset;
                var written = 0;

                do
                {
                    var readCount = _stream.Read(buffer, 0, Math.Min(4096, fileLength - written));
                    if (readCount == 0) throw new EndOfStreamException();

                    fileStream.Write(buffer, 0, readCount);
                    fileStream.Flush();

                    written += readCount;
                } while (written < fileLength);

                totalWritten += written;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
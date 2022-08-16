using System.Buffers;

namespace Fossil_Fighters_Tool.Archive;

public class ReadOnlyMemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public ReadOnlyMemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public ReadOnlyMemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new ReadOnlyMemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };

        Next = segment;

        return segment;
    }
}
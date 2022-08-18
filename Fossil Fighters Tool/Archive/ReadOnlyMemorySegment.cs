using System.Buffers;

namespace Fossil_Fighters_Tool.Archive;

public class ReadOnlyMemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public ReadOnlyMemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public ReadOnlyMemorySegment<T> Add(ReadOnlyMemory<T> memory)
    {
        var segment = new ReadOnlyMemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length
        };

        Next = segment;

        return segment;
    }

    public void Update(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
        
        var previousSegment = this;
        var currentSegment = this;

        while ((currentSegment = currentSegment.Next as ReadOnlyMemorySegment<T>) != null)
        {
            currentSegment.RunningIndex = previousSegment.RunningIndex + previousSegment.Memory.Length;
            previousSegment = currentSegment;
        }
    }
}
using System.Buffers;

namespace Fossil_Fighters_Tool.Archive.Compression.Rle;

public class RleStream : CompressibleStream
{
    private const int CompressionHeader = 0x30;
    
    private const int MaxRawDataLength = (1 << 7) - 1 + 1;
    private const int MinCompressDataLength = 3;
    private const int MaxCompressDataLength = (1 << 7) - 1 + 3;
    
    public RleStream(Stream stream, CompressibleStreamMode mode, bool leaveOpen = false) : base(stream, mode, leaveOpen)
    {
    }
    
    protected override void Decompress(BinaryReader reader, BinaryWriter writer, Stream inputStream, MemoryStream outputStream)
    {
        var rawHeaderData = reader.ReadInt32();
        if ((rawHeaderData & CompressionHeader) != CompressionHeader) throw new InvalidDataException(string.Format(Localization.StreamIsNotCompressedBy, "RLE"));

        var decompressSize = (rawHeaderData >> 8) & 0xFFFFFF;
        outputStream.Capacity = decompressSize;
            
        while (outputStream.Length < decompressSize)
        {
            var flagRawData = reader.ReadByte();
            var flagType = flagRawData >> 7;
            var flagData = flagRawData & 0x7F;

            if (flagType == 0)
            {
                var repeatCount = flagData + 1;
                    
                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(reader.ReadByte());
                }
            }
            else
            {
                var repeatCount = flagData + 3;
                var repeatData = reader.ReadByte();

                for (var i = repeatCount - 1; i >= 0; i--)
                {
                    writer.Write(repeatData);
                }
            }
        }
    }

    protected override void Compress(BinaryReader reader, BinaryWriter writer, MemoryStream inputStream, MemoryStream outputStream)
    {
        int GetNextRepeatCount(Span<byte> buffer)
        {
            if (inputStream.Position >= inputStream.Length) return 0;
                
            var bytesWritten = 0;
            var dataToCheck = reader.ReadByte();
                
            buffer[bytesWritten++] = dataToCheck;
                
            while (bytesWritten < MaxCompressDataLength && inputStream.Position < inputStream.Length)
            {
                if (dataToCheck != reader.ReadByte())
                {
                    inputStream.Seek(-1, SeekOrigin.Current);
                    break;
                }
                
                buffer[bytesWritten++] = dataToCheck;
            }
            
            return bytesWritten;
        }
            
        void WriteCompressed(byte data, int count)
        {
            writer.Write((byte) (1 << 7 | (count - 3)));
            writer.Write(data);
        }
            
        void WriteUncompressed(ReadOnlySpan<byte> buffer)
        {
            writer.Write((byte) (buffer.Length - 1));
            writer.Write(buffer);
        }
        
        writer.Write((uint) (CompressionHeader | inputStream.Length << 8));

        var tempBuffer = ArrayPool<byte>.Shared.Rent(MaxCompressDataLength);
        var rawDataBuffer = ArrayPool<byte>.Shared.Rent(MaxRawDataLength);
            
        try
        {
            int tempBufferLength;
            var rawDataLength = 0;

            while ((tempBufferLength = GetNextRepeatCount(tempBuffer)) > 0)
            {
                if (tempBufferLength >= MinCompressDataLength)
                {
                    if (rawDataLength > 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;
                    }
                        
                    WriteCompressed(tempBuffer[0], tempBufferLength);
                }
                else
                {
                    var rawDataSpaceRemaining = MaxRawDataLength - rawDataLength;

                    if (rawDataSpaceRemaining == 0)
                    {
                        WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                        rawDataLength = 0;
                            
                        tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                        rawDataLength += tempBufferLength;
                    }
                    else
                    {
                        if (tempBufferLength > rawDataSpaceRemaining)
                        {
                            tempBuffer.AsSpan(0, rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;
                            
                            WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
                            rawDataLength = 0;
                            
                            tempBuffer.AsSpan(rawDataSpaceRemaining, tempBufferLength - rawDataSpaceRemaining).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += tempBufferLength - rawDataSpaceRemaining;
                        }
                        else
                        {
                            tempBuffer.AsSpan(0, tempBufferLength).CopyTo(rawDataBuffer.AsSpan(rawDataLength));
                            rawDataLength += rawDataSpaceRemaining;
                        }
                    }
                }
            }
                
            if (rawDataLength > 0)
            {
                WriteUncompressed(rawDataBuffer.AsSpan(0, rawDataLength));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
            ArrayPool<byte>.Shared.Return(rawDataBuffer);
        }
    }
}
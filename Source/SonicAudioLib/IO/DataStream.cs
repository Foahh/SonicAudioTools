using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SonicAudioLib.IO;

public static partial class DataStream
{
    public static void CopyPartTo(Stream source, Stream destination, long length, int bufferSize)
    {
        using var buffer = MemoryPool<byte>.Shared.Rent(bufferSize);
        long copiedBytes = 0;
        var bufferSpan = buffer.Memory.Span;

        while (copiedBytes < length)
        {
            var bytesToRead = (int)Math.Min(bufferSize, length - copiedBytes);
            var num = source.Read(bufferSpan[..bytesToRead]);

            if (num == 0) break;

            destination.Write(bufferSpan[..num]);
            copiedBytes += num;
        }
    }

    public static void CopyPartTo(Stream source, Stream destination, long position, long length, int bufferSize)
    {
        source.Seek(position, SeekOrigin.Begin);
        CopyPartTo(source, destination, length, bufferSize);
    }

    public static byte[] ReadBytes(Stream source, int length)
    {
        var buffer = new byte[length];
        source.ReadExactly(buffer, 0, length);
        return buffer;
    }

    public static byte ReadByte(Stream source)
    {
        Span<byte> buffer = stackalloc byte[1];
        source.ReadExactly(buffer);
        return buffer[0];
    }

    public static sbyte ReadSByte(Stream source)
    {
        Span<byte> buffer = stackalloc byte[1];
        source.ReadExactly(buffer);
        return (sbyte)buffer[0];
    }

    public static bool ReadBoolean(Stream source)
    {
        Span<byte> buffer = stackalloc byte[1];
        source.ReadExactly(buffer);
        return buffer[0] == 1;
    }

    public static ushort ReadUInt16(Stream source)
    {
        Span<byte> buffer = stackalloc byte[2];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public static ushort ReadUInt16Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[2];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public static short ReadInt16(Stream source)
    {
        Span<byte> buffer = stackalloc byte[2];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    public static short ReadInt16Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[2];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    public static uint ReadUInt32(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    public static uint ReadUInt32Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    public static int ReadInt32(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static int ReadInt32Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public static ulong ReadUInt64(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    public static ulong ReadUInt64Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    public static long ReadInt64(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    public static long ReadInt64Be(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public static float ReadSingle(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    public static float ReadSingleBe(Stream source)
    {
        Span<byte> buffer = stackalloc byte[4];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleBigEndian(buffer);
    }

    public static double ReadDouble(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    public static double ReadDoubleBe(Stream source)
    {
        Span<byte> buffer = stackalloc byte[8];
        source.ReadExactly(buffer);
        return BinaryPrimitives.ReadDoubleBigEndian(buffer);
    }

    public static string ReadCString(Stream source)
    {
        return ReadCString(source, Encoding.ASCII);
    }

    public static string ReadCString(Stream source, Encoding? encoding)
    {
        if (encoding is null) throw new ArgumentNullException(nameof(encoding));

        var characters = new List<byte>();

        Span<byte> buffer = stackalloc byte[1];
        source.ReadExactly(buffer);
        while (buffer[0] != 0)
        {
            characters.Add(buffer[0]);
            source.ReadExactly(buffer);
        }

        return encoding.GetString([.. characters]);
    }

    public static string ReadCString(Stream source, int length)
    {
        return ReadCString(source, length, Encoding.ASCII);
    }

    public static string ReadCString(Stream source, int length, Encoding encoding)
    {
        var buffer = length <= 1024 ? stackalloc byte[length] : new byte[length];
        source.ReadExactly(buffer);

        var terminator = buffer.IndexOf((byte)0);

        if (terminator >= 0) return encoding.GetString(buffer[..terminator]);
        return encoding.GetString(buffer);
    }
}

public static partial class DataStream
{
    public static void WriteBytes(Stream destination, byte[] value)
    {
        destination.Write(value);
    }

    public static void WriteBytes(Stream destination, byte[] value, int length)
    {
        destination.Write(value);
    }

    public static void WriteByte(Stream destination, byte value)
    {
        destination.WriteByte(value);
    }

    public static void WriteByteAt(Stream destination, byte value, long position)
    {
        var oldPosition = destination.Position;
        destination.Seek(position, SeekOrigin.Begin);

        WriteByte(destination, value);
        destination.Seek(oldPosition, SeekOrigin.Begin);
    }

    public static void WriteBoolean(Stream destination, bool value)
    {
        destination.WriteByte((byte)(value ? 1 : 0));
    }

    public static void WriteSByte(Stream destination, sbyte value)
    {
        destination.WriteByte((byte)value);
    }


    public static void WriteUInt16(Stream destination, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteUInt16Be(Stream destination, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteInt16(Stream destination, short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteInt16Be(Stream destination, short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        destination.Write(buffer);
    }


    public static void WriteUInt32(Stream destination, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteUInt32Be(Stream destination, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        destination.Write(buffer);
    }


    public static void WriteInt32(Stream destination, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteInt32Be(Stream destination, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        destination.Write(buffer);
    }


    public static void WriteUInt64(Stream destination, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteUInt64Be(Stream destination, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteInt64(Stream destination, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteInt64Be(Stream destination, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteSingle(Stream destination, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteSingleBe(Stream destination, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteDouble(Stream destination, double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteDoubleBe(Stream destination, double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        destination.Write(buffer);
    }

    public static void WriteCString(Stream destination, string value)
    {
        WriteCString(destination, value, Encoding.ASCII);
    }

    public static void WriteCString(Stream destination, string value, Encoding encoding)
    {
        var buffer = encoding.GetBytes(value);
        destination.Write(buffer);
        destination.WriteByte(0);
    }

    public static void WriteCString(Stream destination, string value, int length)
    {
        WriteCString(destination, value, length, Encoding.ASCII);
    }

    public static void WriteCString(Stream destination, string value, int length, Encoding encoding)
    {
        var buffer = encoding.GetBytes(value);
        destination.Write(buffer);
    }

    public static void Pad(Stream destination, long alignment)
    {
        var remainder = destination.Position % alignment;
        if (remainder == 0) return;

        var padding = (int)(alignment - remainder);

        if (padding <= 1024)
        {
            Span<byte> buffer = stackalloc byte[padding];
            buffer.Clear();
            destination.Write(buffer);
        }
        else
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(padding);
            var span = buffer.Memory.Span[..padding];
            span.Clear();
            destination.Write(span);
        }
    }
}
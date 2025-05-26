using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SonicAudioLib.IO;

public static class DataStream
{
    // Not thread safe... but I guess it's OK.
    private static readonly byte[] Buffer = new byte[8];

    public static void CopyTo(Stream source, Stream destination)
    {
        CopyTo(source, destination, 4096);
    }

    public static void CopyTo(Stream source, Stream destination, int bufferSize)
    {
        int read;
        var buffer = new byte[bufferSize];

        while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
        {
            destination.Write(buffer, 0, read);
        }
    }

    public static void CopyPartTo(Stream source, Stream destination, long length, int bufferSize)
    {
        int num;
        var buffer = new byte[bufferSize];

        long copiedBytes = 0;
        while (copiedBytes < length && (num = source.Read(buffer, 0, bufferSize)) != 0)
        {
            if (copiedBytes + num >= length)
            {
                num = (int)(length - copiedBytes);
            }

            copiedBytes += num;
            destination.Write(buffer, 0, num);
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

    public static byte[] ReadBytesAt(Stream source, int length, long position)
    {
        var oldPosition = source.Position;
        source.Seek(position, SeekOrigin.Begin);
        var result = ReadBytes(source, length);
        source.Seek(oldPosition, SeekOrigin.Begin);

        return result;
    }

    public static void WriteBytes(Stream destination, byte[] value)
    {
        destination.Write(value, 0, value.Length);
    }

    public static void WriteBytes(Stream destination, byte[] value, int length)
    {
        destination.Write(value, 0, length);
    }

    public static byte ReadByte(Stream source)
    {
        source.ReadExactly(Buffer, 0, 1);

        return Buffer[0];
    }

    public static byte ReadByteAt(Stream source, long position)
    {
        var oldPosition = source.Position;
        source.Seek(position, SeekOrigin.Begin);

        var value = ReadByte(source);
        source.Seek(oldPosition, SeekOrigin.Begin);

        return value;
    }

    public static void WriteByte(Stream destination, byte value)
    {
        Buffer[0] = value;

        destination.Write(Buffer, 0, 1);
    }

    public static void WriteByteAt(Stream destination, byte value, long position)
    {
        var oldPosition = destination.Position;
        destination.Seek(position, SeekOrigin.Begin);

        WriteByte(destination, value);
        destination.Seek(oldPosition, SeekOrigin.Begin);
    }

    public static bool ReadBoolean(Stream source)
    {
        source.ReadExactly(Buffer, 0, 1);

        return Buffer[0] == 1;
    }

    public static void WriteBoolean(Stream destination, bool value)
    {
        Buffer[0] = (byte)(value ? 1 : 0);

        destination.Write(Buffer, 0, 1);
    }

    public static sbyte ReadSByte(Stream source)
    {
        source.ReadExactly(Buffer, 0, 1);

        return (sbyte)Buffer[0];
    }

    public static void WriteSByte(Stream destination, sbyte value)
    {
        Buffer[0] = (byte)value;

        destination.Write(Buffer, 0, 1);
    }

    public static ushort ReadUInt16(Stream source)
    {
        source.ReadExactly(Buffer, 0, 2);

        return (ushort)(Buffer[0] | Buffer[1] << 8);
    }

    public static ushort ReadUInt16Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 2);

        return (ushort)(Buffer[0] << 8 | Buffer[1]);
    }

    public static void WriteUInt16(Stream destination, ushort value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);

        destination.Write(Buffer, 0, 2);
    }

    public static void WriteUInt16Be(Stream destination, ushort value)
    {
        Buffer[0] = (byte)(value >> 8);
        Buffer[1] = (byte)value;

        destination.Write(Buffer, 0, 2);
    }

    public static short ReadInt16(Stream source)
    {
        source.ReadExactly(Buffer, 0, 2);

        return (short)(Buffer[0] | Buffer[1] << 8);
    }

    public static short ReadInt16Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 2);

        return (short)(Buffer[0] << 8 | Buffer[1]);
    }

    public static void WriteInt16(Stream destination, short value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);

        destination.Write(Buffer, 0, 2);
    }

    public static void WriteInt16Be(Stream destination, short value)
    {
        Buffer[0] = (byte)(value >> 8);
        Buffer[1] = (byte)value;

        destination.Write(Buffer, 0, 2);
    }

    public static uint ReadUInt32(Stream source)
    {
        source.ReadExactly(Buffer, 0, 4);

        return (uint)(Buffer[0] | Buffer[1] << 8 | Buffer[2] << 16 | Buffer[3] << 24);
    }

    public static uint ReadUInt32Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 4);

        return (uint)(Buffer[0] << 24 | Buffer[1] << 16 | Buffer[2] << 8 | Buffer[3]);
    }

    public static void WriteUInt32(Stream destination, uint value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);
        Buffer[2] = (byte)(value >> 16);
        Buffer[3] = (byte)(value >> 24);

        destination.Write(Buffer, 0, 4);
    }

    public static void WriteUInt32Be(Stream destination, uint value)
    {
        Buffer[0] = (byte)(value >> 24);
        Buffer[1] = (byte)(value >> 16);
        Buffer[2] = (byte)(value >> 8);
        Buffer[3] = (byte)value;

        destination.Write(Buffer, 0, 4);
    }

    public static int ReadInt32(Stream source)
    {
        source.ReadExactly(Buffer, 0, 4);

        return Buffer[0] | Buffer[1] << 8 | Buffer[2] << 16 | Buffer[3] << 24;
    }

    public static int ReadInt32Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 4);

        return Buffer[0] << 24 | Buffer[1] << 16 | Buffer[2] << 8 | Buffer[3];
    }

    public static void WriteInt32(Stream destination, int value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);
        Buffer[2] = (byte)(value >> 16);
        Buffer[3] = (byte)(value >> 24);

        destination.Write(Buffer, 0, 4);
    }

    public static void WriteInt32Be(Stream destination, int value)
    {
        Buffer[0] = (byte)(value >> 24);
        Buffer[1] = (byte)(value >> 16);
        Buffer[2] = (byte)(value >> 8);
        Buffer[3] = (byte)value;

        destination.Write(Buffer, 0, 4);
    }

    public static ulong ReadUInt64(Stream source)
    {
        source.ReadExactly(Buffer, 0, 8);

        return Buffer[0] | (ulong)Buffer[1] << 8 |
               (ulong)Buffer[2] << 16 | (ulong)Buffer[3] << 24 |
               (ulong)Buffer[4] << 32 | (ulong)Buffer[5] << 40 |
               (ulong)Buffer[6] << 48 | (ulong)Buffer[7] << 56;
    }

    public static ulong ReadUInt64Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 8);

        return (ulong)Buffer[0] << 56 | (ulong)Buffer[1] << 48 |
               (ulong)Buffer[2] << 40 | (ulong)Buffer[3] << 32 |
               (ulong)Buffer[4] << 24 | (ulong)Buffer[5] << 16 |
               (ulong)Buffer[6] << 8 | Buffer[7];
    }

    public static void WriteUInt64(Stream destination, ulong value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);
        Buffer[2] = (byte)(value >> 16);
        Buffer[3] = (byte)(value >> 24);
        Buffer[4] = (byte)(value >> 32);
        Buffer[5] = (byte)(value >> 40);
        Buffer[6] = (byte)(value >> 48);
        Buffer[7] = (byte)(value >> 56);

        destination.Write(Buffer, 0, 8);
    }

    public static void WriteUInt64Be(Stream destination, ulong value)
    {
        Buffer[0] = (byte)(value >> 56);
        Buffer[1] = (byte)(value >> 48);
        Buffer[2] = (byte)(value >> 40);
        Buffer[3] = (byte)(value >> 32);
        Buffer[4] = (byte)(value >> 24);
        Buffer[5] = (byte)(value >> 16);
        Buffer[6] = (byte)(value >> 8);
        Buffer[7] = (byte)value;

        destination.Write(Buffer, 0, 8);
    }

    public static long ReadInt64(Stream source)
    {
        source.ReadExactly(Buffer, 0, 8);

        return Buffer[0] | Buffer[1] << 8 |
               Buffer[2] << 16 | Buffer[3] << 24 |
               Buffer[4] << 32 | Buffer[5] << 40 |
               Buffer[6] << 48 | Buffer[7] << 56;
    }

    public static long ReadInt64Be(Stream source)
    {
        source.ReadExactly(Buffer, 0, 8);

        return Buffer[0] << 56 | Buffer[1] << 48 |
               Buffer[2] << 40 | Buffer[3] << 32 |
               Buffer[4] << 24 | Buffer[5] << 16 |
               Buffer[6] << 8 | Buffer[7];
    }

    public static void WriteInt64(Stream destination, long value)
    {
        Buffer[0] = (byte)value;
        Buffer[1] = (byte)(value >> 8);
        Buffer[2] = (byte)(value >> 16);
        Buffer[3] = (byte)(value >> 24);
        Buffer[4] = (byte)(value >> 32);
        Buffer[5] = (byte)(value >> 40);
        Buffer[6] = (byte)(value >> 48);
        Buffer[7] = (byte)(value >> 56);

        destination.Write(Buffer, 0, 8);
    }

    public static void WriteInt64Be(Stream destination, long value)
    {
        Buffer[0] = (byte)(value >> 56);
        Buffer[1] = (byte)(value >> 48);
        Buffer[2] = (byte)(value >> 40);
        Buffer[3] = (byte)(value >> 32);
        Buffer[4] = (byte)(value >> 24);
        Buffer[5] = (byte)(value >> 16);
        Buffer[6] = (byte)(value >> 8);
        Buffer[7] = (byte)value;

        destination.Write(Buffer, 0, 8);
    }

    public static float ReadSingle(Stream source)
    {
        var union = new SingleUnion();
        union.UInt = ReadUInt32(source);

        return union.Single;
    }

    public static float ReadSingleBe(Stream source)
    {
        var union = new SingleUnion();
        union.UInt = ReadUInt32Be(source);

        return union.Single;
    }

    public static void WriteSingle(Stream destination, float value)
    {
        var union = new SingleUnion();
        union.Single = value;

        WriteUInt32(destination, union.UInt);
    }

    public static void WriteSingleBe(Stream destination, float value)
    {
        var union = new SingleUnion();
        union.Single = value;

        WriteUInt32Be(destination, union.UInt);
    }

    public static double ReadDouble(Stream source)
    {
        var union = new DoubleUnion();
        union.ULong = ReadUInt64(source);

        return union.Double;
    }

    public static double ReadDoubleBe(Stream source)
    {
        var union = new DoubleUnion();
        union.ULong = ReadUInt64Be(source);

        return union.Double;
    }

    public static void WriteDouble(Stream destination, double value)
    {
        var union = new DoubleUnion();
        union.Double = value;

        WriteUInt64(destination, union.ULong);
    }

    public static void WriteDoubleBe(Stream destination, double value)
    {
        var union = new DoubleUnion();
        union.Double = value;

        WriteUInt64Be(destination, union.ULong);
    }

    public static string ReadCString(Stream source)
    {
        return ReadCString(source, Encoding.ASCII);
    }

    public static string ReadCString(Stream source, Encoding encoding)
    {
        var characters = new List<byte>();

        source.ReadExactly(Buffer, 0, 1);
        while (Buffer[0] != 0)
        {
            characters.Add(Buffer[0]);
            source.ReadExactly(Buffer, 0, 1);
        }

        return encoding.GetString([.. characters]);
    }

    public static void WriteCString(Stream destination, string value)
    {
        WriteCString(destination, value, Encoding.ASCII);
    }

    public static void WriteCString(Stream destination, string value, Encoding encoding)
    {
        var buffer = encoding.GetBytes(value);

        destination.Write(buffer, 0, buffer.Length);

        buffer[0] = 0;
        destination.Write(buffer, 0, 1);
    }

    public static string ReadCString(Stream source, int length)
    {
        return ReadCString(source, length, Encoding.ASCII);
    }

    public static string ReadCString(Stream source, int length, Encoding encoding)
    {
        var buffer = new byte[length];
        source.ReadExactly(buffer, 0, length);

        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 0)
            {
                return encoding.GetString(buffer, 0, i);
            }
        }

        return encoding.GetString(buffer);
    }

    public static void WriteCString(Stream destination, string value, int length)
    {
        WriteCString(destination, value, length, Encoding.ASCII);
    }

    public static void WriteCString(Stream destination, string value, int length, Encoding encoding)
    {
        var buffer = encoding.GetBytes(value.ToCharArray(), 0, length);
        destination.Write(buffer, 0, length);
    }

    public static void Pad(Stream destination, long alignment)
    {
        while (destination.Position % alignment != 0)
        {
            WriteByte(destination, 0);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct SingleUnion
    {
        [FieldOffset(0)]
        public float Single;

        [FieldOffset(0)]
        public uint UInt;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DoubleUnion
    {
        [FieldOffset(0)]
        public double Double;

        [FieldOffset(0)]
        public ulong ULong;
    }
}
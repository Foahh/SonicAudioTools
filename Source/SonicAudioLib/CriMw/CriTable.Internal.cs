using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SonicAudioLib.CriMw;

[StructLayout(LayoutKind.Sequential)]
internal struct CriTableHeader
{
    public static readonly byte[] SignatureBytes = "@UTF"u8.ToArray();

    public const byte EncodingTypeShiftJis = 0;
    public const byte EncodingTypeUtf8 = 1;

    public byte[] Signature;
    public uint Length;
    public byte UnknownByte;
    public byte EncodingType;
    public ushort RowsPosition;
    public uint StringPoolPosition;
    public uint DataPoolPosition;
    public uint TableNamePosition;
    public string TableName;
    public ushort FieldCount;
    public ushort RowLength;
    public uint RowCount;
}

[Flags]
internal enum CriFieldFlags : byte
{
    Name = 16,
    DefaultValue = 32,
    RowStorage = 64,

    Byte = 0,
    SByte = 1,
    UInt16 = 2,
    Int16 = 3,
    UInt32 = 4,
    Int32 = 5,
    UInt64 = 6,
    Int64 = 7,
    Single = 8,
    Double = 9,
    String = 10,
    Data = 11,
    Guid = 12,

    TypeMask = 15
}

[StructLayout(LayoutKind.Sequential)]
internal struct CriTableField
{
    public CriFieldFlags Flags;
    public string Name;
    public uint Position;
    public uint Length;
    public uint Offset;
    public object? Value;
}

internal static class CriTableMasker
{
    public static void FindKeys(byte[] signature, out uint xor, out uint xorMultiplier)
    {
        for (var x = 0; x < 0x100; x++)
        {
            // Find XOR using first byte
            if ((signature[0] ^ (byte)x) != CriTableHeader.SignatureBytes[0]) continue;

            // Matched the first byte, try finding the multiplier with the second byte
            for (var m = 0; m < 0x100; m++)
            {
                // Matched the second byte, now make sure the other bytes match as well
                if ((signature[1] ^ (byte)(x * m)) != CriTableHeader.SignatureBytes[1]) continue;
                var b = (byte)(x * m);

                var allMatches = true;
                for (var i = 2; i < 4; i++)
                {
                    b = (byte)(b * m);
                    if ((signature[i] ^ b) == CriTableHeader.SignatureBytes[i]) continue;
                    allMatches = false;
                    break;
                }

                // All matches, return the xor and multiplier
                if (!allMatches) continue;
                xor = (uint)x;
                xorMultiplier = (uint)m;
                return;
            }
        }

        throw new InvalidDataException("'@UTF' signature could not be found. Your file might be corrupted.");
    }

    public static void Mask(Stream source, Stream destination, long length, uint xor, uint xorMultiplier)
    {
        var currentXor = xor;
        var currentPosition = source.Position;

        while (source.Position < currentPosition + length)
        {
            var maskedByte = (byte)(source.ReadByte() ^ currentXor);
            currentXor *= xorMultiplier;

            destination.WriteByte(maskedByte);
        }
    }

    public static void Mask(Stream source, long length, uint xor, uint xorMultiplier)
    {
        if (!source.CanRead || !source.CanWrite) return;

        var currentXor = xor;
        var currentPosition = source.Position;

        while (source.Position < currentPosition + length)
        {
            var maskedByte = (byte)(source.ReadByte() ^ currentXor);
            currentXor *= xorMultiplier;

            source.Position--;
            source.WriteByte(maskedByte);
        }
    }
}
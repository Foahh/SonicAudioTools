using SonicAudioLib.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonicAudioLib.CriMw;

public class CriTableReader : IDisposable
{
    private readonly List<CriTableField> fields;
    private readonly bool leaveOpen;
    private CriTableHeader header;
    private long headerPosition;

    private CriTableReader(Stream source, bool leaveOpen)
    {
        SourceStream = source;
        header = new CriTableHeader();
        fields =
        [
        ];
        this.leaveOpen = leaveOpen;

        ReadTable();
    }

    public object this[int fieldIndex] => GetValue(fieldIndex);

    public object this[string fieldName] => GetValue(fieldName);

    public ushort NumberOfFields => header.FieldCount;

    public uint NumberOfRows => header.RowCount;

    public string TableName => header.TableName;

    public long CurrentRow { get; private set; } = -1;

    public Stream SourceStream { get; private set; }

    public Encoding EncodingType { get; private set; }

    public void Dispose()
    {
        fields.Clear();

        if (!leaveOpen)
        {
            SourceStream.Close();
        }
    }

    private void ReadTable()
    {
        headerPosition = SourceStream.Position;

        if (!(header.Signature = DataStream.ReadBytes(SourceStream, 4)).SequenceEqual(CriTableHeader.SignatureBytes))
        {
            var unmaskedSource = new MemoryStream();

            CriTableMasker.FindKeys(header.Signature, out var x, out var m);

            SourceStream.Seek(headerPosition, SeekOrigin.Begin);
            CriTableMasker.Mask(SourceStream, unmaskedSource, SourceStream.Length, x, m);

            // Close the old stream
            if (!leaveOpen)
            {
                SourceStream.Close();
            }

            SourceStream = unmaskedSource;
            SourceStream.Seek(4, SeekOrigin.Begin);
        }

        header.Length = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        header.UnknownByte = DataStream.ReadByte(SourceStream);
        header.EncodingType = DataStream.ReadByte(SourceStream);

        if (header.UnknownByte != 0)
        {
            throw new InvalidDataException($"Invalid byte ({header.UnknownByte}. Please report this error with the file(s).");
        }

        switch (header.EncodingType)
        {
            case CriTableHeader.EncodingTypeShiftJis:
                EncodingType = Encoding.GetEncoding("shift-jis");
                break;

            case CriTableHeader.EncodingTypeUtf8:
                EncodingType = Encoding.UTF8;
                break;

            default:
                throw new InvalidDataException($"Unknown encoding type ({header.EncodingType}). Please report this error with the file(s).");
        }

        header.RowsPosition = (ushort)(DataStream.ReadUInt16Be(SourceStream) + 0x8);
        header.StringPoolPosition = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        header.DataPoolPosition = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        header.TableName = ReadString();
        header.FieldCount = DataStream.ReadUInt16Be(SourceStream);
        header.RowLength = DataStream.ReadUInt16Be(SourceStream);
        header.RowCount = DataStream.ReadUInt32Be(SourceStream);

        uint offset = 0;
        for (ushort i = 0; i < header.FieldCount; i++)
        {
            var field = new CriTableField();
            field.Flag = (CriFieldFlag)DataStream.ReadByte(SourceStream);

            if (field.Flag.HasFlag(CriFieldFlag.Name))
            {
                field.Name = ReadString();
            }

            if (field.Flag.HasFlag(CriFieldFlag.DefaultValue))
            {
                if (field.Flag.HasFlag(CriFieldFlag.Data))
                {
                    field.Position = DataStream.ReadUInt32Be(SourceStream);
                    field.Length = DataStream.ReadUInt32Be(SourceStream);
                }

                else
                {
                    field.Value = ReadValue(field.Flag);
                }
            }

            // Not even per row, and not even constant value? Then there's no storage.
            else if (!field.Flag.HasFlag(CriFieldFlag.RowStorage) && !field.Flag.HasFlag(CriFieldFlag.DefaultValue))
            {
                field.Value = CriField.NullValues[(byte)field.Flag & 0x0F];
            }

            // Row storage, calculate the offset
            if (field.Flag.HasFlag(CriFieldFlag.RowStorage))
            {
                field.Offset = offset;

                switch (field.Flag & CriFieldFlag.TypeMask)
                {
                    case CriFieldFlag.Byte:
                    case CriFieldFlag.SByte:
                        offset += 1;
                        break;
                    case CriFieldFlag.Int16:
                    case CriFieldFlag.UInt16:
                        offset += 2;
                        break;
                    case CriFieldFlag.Int32:
                    case CriFieldFlag.UInt32:
                    case CriFieldFlag.Single:
                    case CriFieldFlag.String:
                        offset += 4;
                        break;
                    case CriFieldFlag.Int64:
                    case CriFieldFlag.UInt64:
                    case CriFieldFlag.Double:
                    case CriFieldFlag.Data:
                        offset += 8;
                        break;
                }
            }

            fields.Add(field);
        }
    }

    public string GetFieldName(int fieldIndex)
    {
        return fields[fieldIndex].Name;
    }

    public Type GetFieldType(int fieldIndex)
    {
        return CriField.FieldTypes[(byte)fields[fieldIndex].Flag & 0x0F];
    }

    public Type GetFieldType(string fieldName)
    {
        return CriField.FieldTypes[(byte)fields[GetFieldIndex(fieldName)].Flag & 0x0F];
    }

    public object GetFieldValue(int fieldIndex)
    {
        return fields[fieldIndex].Value;
    }

    internal CriFieldFlag GetFieldFlag(string fieldName)
    {
        return fields[GetFieldIndex(fieldName)].Flag;
    }

    internal CriFieldFlag GetFieldFlag(int fieldIndex)
    {
        return fields[fieldIndex].Flag;
    }

    public object GetFieldValue(string fieldName)
    {
        return fields[GetFieldIndex(fieldName)].Value;
    }

    public CriField GetField(int fieldIndex)
    {
        return new CriField(GetFieldName(fieldIndex), GetFieldType(fieldIndex), GetFieldValue(fieldIndex));
    }

    public CriField GetField(string fieldName)
    {
        return new CriField(fieldName, GetFieldType(fieldName), GetFieldValue(fieldName));
    }

    public int GetFieldIndex(string fieldName)
    {
        return fields.FindIndex(field => field.Name == fieldName);
    }

    public bool ContainsField(string fieldName)
    {
        return fields.Exists(field => field.Name == fieldName);
    }

    private void GoToValue(int fieldIndex)
    {
        SourceStream.Seek(headerPosition + header.RowsPosition + header.RowLength * CurrentRow + fields[fieldIndex].Offset, SeekOrigin.Begin);
    }

    public bool Read()
    {
        if (CurrentRow + 1 >= header.RowCount)
        {
            return false;
        }

        CurrentRow++;
        return true;
    }

    public bool MoveToRow(long rowIndex)
    {
        if (rowIndex >= header.RowCount)
        {
            return false;
        }

        CurrentRow = rowIndex;
        return true;
    }

    public object[] GetValueArray()
    {
        var values = new object[header.FieldCount];

        for (var i = 0; i < header.FieldCount; i++)
        {
            if (fields[i].Flag.HasFlag(CriFieldFlag.Data))
            {
                values[i] = GetData(i);
            }

            else
            {
                values[i] = GetValue(i);
            }
        }

        return values;
    }

    public IEnumerable GetValues()
    {
        for (var i = 0; i < header.FieldCount; i++)
        {
            yield return GetValue(i);
        }
    }

    public object GetValue(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= fields.Count)
        {
            return null;
        }

        if (!fields[fieldIndex].Flag.HasFlag(CriFieldFlag.RowStorage))
        {
            if (fields[fieldIndex].Flag.HasFlag(CriFieldFlag.Data))
            {
                return new SubStream(SourceStream, 0, 0);
            }

            return fields[fieldIndex].Value;
        }

        GoToValue(fieldIndex);
        return ReadValue(fields[fieldIndex].Flag);
    }

    public object GetValue(string fieldName)
    {
        return GetValue(GetFieldIndex(fieldName));
    }

    public T GetValue<T>(int fieldIndex)
    {
        return (T)GetValue(fieldIndex);
    }

    public T GetValue<T>(string fieldName)
    {
        return (T)GetValue(fieldName);
    }

    public byte GetByte(int fieldIndex)
    {
        return (byte)GetValue(fieldIndex);
    }

    public byte GetByte(string fieldName)
    {
        return (byte)GetValue(fieldName);
    }

    public sbyte GetSByte(int fieldIndex)
    {
        return (sbyte)GetValue(fieldIndex);
    }

    public sbyte GetSByte(string fieldName)
    {
        return (sbyte)GetValue(fieldName);
    }

    public ushort GetUInt16(int fieldIndex)
    {
        return (ushort)GetValue(fieldIndex);
    }

    public ushort GetUInt16(string fieldName)
    {
        return (ushort)GetValue(fieldName);
    }

    public short GetInt16(int fieldIndex)
    {
        return (short)GetValue(fieldIndex);
    }

    public short GetInt16(string fieldName)
    {
        return (short)GetValue(fieldName);
    }

    public uint GetUInt32(int fieldIndex)
    {
        return (uint)GetValue(fieldIndex);
    }

    public uint GetUInt32(string fieldName)
    {
        return (uint)GetValue(fieldName);
    }

    public int GetInt32(int fieldIndex)
    {
        return (int)GetValue(fieldIndex);
    }

    public int GetInt32(string fieldName)
    {
        return (int)GetValue(fieldName);
    }

    public ulong GetUInt64(int fieldIndex)
    {
        return (ulong)GetValue(fieldIndex);
    }

    public ulong GetUInt64(string fieldName)
    {
        return (ulong)GetValue(fieldName);
    }

    public long GetInt64(int fieldIndex)
    {
        return (long)GetValue(fieldIndex);
    }

    public long GetInt64(string fieldName)
    {
        return (long)GetValue(fieldName);
    }

    public float GetSingle(int fieldIndex)
    {
        return (float)GetValue(fieldIndex);
    }

    public float GetSingle(string fieldName)
    {
        return (float)GetValue(fieldName);
    }

    public double GetDouble(int fieldIndex)
    {
        return (double)GetValue(fieldIndex);
    }

    public double GetDouble(string fieldName)
    {
        return (double)GetValue(fieldName);
    }

    public string GetString(int fieldIndex)
    {
        return (string)GetValue(fieldIndex);
    }

    public string GetString(string fieldName)
    {
        return (string)GetValue(fieldName);
    }

    public SubStream GetSubStream(int fieldIndex)
    {
        return (SubStream)GetValue(fieldIndex);
    }

    public SubStream GetSubStream(string fieldName)
    {
        return (SubStream)GetValue(fieldName);
    }

    public byte[] GetData(int fieldIndex)
    {
        using var ss = GetSubStream(fieldIndex);
        return ss.ToArray();
    }

    public byte[] GetData(string fieldName)
    {
        return GetData(GetFieldIndex(fieldName));
    }

    public CriTableReader GetTableReader(string fieldName)
    {
        return new CriTableReader(GetSubStream(fieldName), false);
    }

    public CriTableReader GetTableReader(int fieldIndex)
    {
        return new CriTableReader(GetSubStream(fieldIndex), false);
    }

    public uint GetLength(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= fields.Count)
        {
            return 0;
        }

        if (!fields[fieldIndex].Flag.HasFlag(CriFieldFlag.RowStorage))
        {
            return fields[fieldIndex].Length;
        }

        GoToValue(fieldIndex);

        SourceStream.Seek(4, SeekOrigin.Current);
        return DataStream.ReadUInt32Be(SourceStream);
    }

    public uint GetLength(string fieldName)
    {
        return GetLength(GetFieldIndex(fieldName));
    }

    public uint GetPosition(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= fields.Count)
        {
            return 0;
        }

        if (!fields[fieldIndex].Flag.HasFlag(CriFieldFlag.RowStorage))
        {
            return fields[fieldIndex].Position;
        }

        GoToValue(fieldIndex);
        return (uint)(headerPosition + header.DataPoolPosition + DataStream.ReadUInt32Be(SourceStream));
    }

    public uint GetPosition(string fieldName)
    {
        return GetPosition(GetFieldIndex(fieldName));
    }

    public bool GetBoolean(int fieldIndex)
    {
        return (byte)GetValue(fieldIndex) > 0;
    }

    public bool GetBoolean(string fieldName)
    {
        return (byte)GetValue(fieldName) > 0;
    }

    public Guid GetGuid(int fieldIndex)
    {
        return (Guid)GetValue(fieldIndex);
    }

    public Guid GetGuid(string fieldName)
    {
        return (Guid)GetValue(fieldName);
    }

    private string ReadString()
    {
        var stringPosition = DataStream.ReadUInt32Be(SourceStream);
        var previousPosition = SourceStream.Position;

        SourceStream.Seek(headerPosition + header.StringPoolPosition + stringPosition, SeekOrigin.Begin);
        var readString = DataStream.ReadCString(SourceStream, EncodingType);

        SourceStream.Seek(previousPosition, SeekOrigin.Begin);

        if (readString == StringPool.AdxBlankString || readString == header.TableName && stringPosition == 0)
        {
            return string.Empty;
        }

        return readString;
    }

    private object ReadValue(CriFieldFlag fieldFlag)
    {
        switch (fieldFlag & CriFieldFlag.TypeMask)
        {
            case CriFieldFlag.Byte:
                return DataStream.ReadByte(SourceStream);
            case CriFieldFlag.SByte:
                return DataStream.ReadSByte(SourceStream);
            case CriFieldFlag.UInt16:
                return DataStream.ReadUInt16Be(SourceStream);
            case CriFieldFlag.Int16:
                return DataStream.ReadInt16Be(SourceStream);
            case CriFieldFlag.UInt32:
                return DataStream.ReadUInt32Be(SourceStream);
            case CriFieldFlag.Int32:
                return DataStream.ReadInt32Be(SourceStream);
            case CriFieldFlag.UInt64:
                return DataStream.ReadUInt64Be(SourceStream);
            case CriFieldFlag.Int64:
                return DataStream.ReadInt64Be(SourceStream);
            case CriFieldFlag.Single:
                return DataStream.ReadSingleBe(SourceStream);
            case CriFieldFlag.Double:
                return DataStream.ReadDoubleBe(SourceStream);
            case CriFieldFlag.String:
                return ReadString();
            case CriFieldFlag.Data:
                {
                    var position = DataStream.ReadUInt32Be(SourceStream);
                    var length = DataStream.ReadUInt32Be(SourceStream);

                    // Some ACB files have the length info set to zero for UTF table fields, so find the correct length
                    if (position > 0 && length == 0)
                    {
                        SourceStream.Seek(headerPosition + header.DataPoolPosition + position, SeekOrigin.Begin);

                        if (DataStream.ReadBytes(SourceStream, 4).SequenceEqual(CriTableHeader.SignatureBytes))
                        {
                            length = DataStream.ReadUInt32Be(SourceStream) + 8;
                        }
                    }

                    return new SubStream(SourceStream, headerPosition + header.DataPoolPosition + position, length);
                }
            case CriFieldFlag.Guid:
                return new Guid(DataStream.ReadBytes(SourceStream, 16));
        }

        return null;
    }

    public static CriTableReader Create(byte[] sourceByteArray)
    {
        Stream source = new MemoryStream(sourceByteArray);
        return Create(source);
    }

    public static CriTableReader Create(string sourceFileName)
    {
        Stream source = File.OpenRead(sourceFileName);
        return Create(source);
    }

    public static CriTableReader Create(Stream source)
    {
        return Create(source, false);
    }

    public static CriTableReader Create(Stream source, bool leaveOpen)
    {
        return new CriTableReader(source, leaveOpen);
    }
}
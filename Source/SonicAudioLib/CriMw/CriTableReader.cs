using SonicAudioLib.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SonicAudioLib.CriMw;

public sealed class CriTableReader : IDisposable
{
    private readonly List<CriTableField> _fields;
    private readonly bool _leaveOpen;
    private CriTableHeader _header;
    private long _headerPosition;

    private CriTableReader(Stream source, bool leaveOpen)
    {
        SourceStream = source;
        _header = new CriTableHeader();
        _fields = [];
        this._leaveOpen = leaveOpen;

        ReadTable();
    }

    public object? this[int fieldIndex] => GetValue(fieldIndex);

    public object? this[string fieldName] => GetValue(fieldName);

    public ushort NumberOfFields => _header.FieldCount;

    public uint NumberOfRows => _header.RowCount;

    public string TableName => _header.TableName;

    public long CurrentRow { get; private set; } = -1;

    public Stream SourceStream { get; private set; }

    public Encoding? EncodingType { get; private set; }

    public void Dispose()
    {
        _fields.Clear();

        if (!_leaveOpen)
        {
            SourceStream.Close();
        }
    }

    private void ReadTable()
    {
        _headerPosition = SourceStream.Position;

        if (!(_header.Signature = DataStream.ReadBytes(SourceStream, 4)).SequenceEqual(CriTableHeader.SignatureBytes))
        {
            var unmaskedSource = new MemoryStream();

            CriTableMasker.FindKeys(_header.Signature, out var x, out var m);

            SourceStream.Seek(_headerPosition, SeekOrigin.Begin);
            CriTableMasker.Mask(SourceStream, unmaskedSource, SourceStream.Length, x, m);

            // Close the old stream
            if (!_leaveOpen)
            {
                SourceStream.Close();
            }

            SourceStream = unmaskedSource;
            SourceStream.Seek(4, SeekOrigin.Begin);
        }

        _header.Length = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        _header.UnknownByte = DataStream.ReadByte(SourceStream);
        _header.EncodingType = DataStream.ReadByte(SourceStream);

        if (_header.UnknownByte != 0)
        {
            throw new InvalidDataException($"Invalid byte ({_header.UnknownByte}. Please report this error with the file(s).");
        }

        switch (_header.EncodingType)
        {
            case CriTableHeader.EncodingTypeShiftJis:
                EncodingType = Encoding.GetEncoding("shift-jis");
                break;

            case CriTableHeader.EncodingTypeUtf8:
                EncodingType = Encoding.UTF8;
                break;

            default:
                throw new InvalidDataException($"Unknown encoding type ({_header.EncodingType}). Please report this error with the file(s).");
        }

        _header.RowsPosition = (ushort)(DataStream.ReadUInt16Be(SourceStream) + 0x8);
        _header.StringPoolPosition = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        _header.DataPoolPosition = DataStream.ReadUInt32Be(SourceStream) + 0x8;
        _header.TableName = ReadString();
        _header.FieldCount = DataStream.ReadUInt16Be(SourceStream);
        _header.RowLength = DataStream.ReadUInt16Be(SourceStream);
        _header.RowCount = DataStream.ReadUInt32Be(SourceStream);

        uint offset = 0;
        for (ushort i = 0; i < _header.FieldCount; i++)
        {
            var field = new CriTableField();
            field.Flags = (CriFieldFlags)DataStream.ReadByte(SourceStream);

            if (field.Flags.HasFlag(CriFieldFlags.Name))
            {
                field.Name = ReadString();
            }

            if (field.Flags.HasFlag(CriFieldFlags.DefaultValue))
            {
                if (field.Flags.HasFlag(CriFieldFlags.Data))
                {
                    field.Position = DataStream.ReadUInt32Be(SourceStream);
                    field.Length = DataStream.ReadUInt32Be(SourceStream);
                }

                else
                {
                    field.Value = ReadValue(field.Flags);
                }
            }

            // Not even per row, and not even constant value? Then there's no storage.
            else if (!field.Flags.HasFlag(CriFieldFlags.RowStorage) && !field.Flags.HasFlag(CriFieldFlags.DefaultValue))
            {
                field.Value = CriField.NullValues[(byte)field.Flags & 0x0F];
            }

            // Row storage, calculate the offset
            if (field.Flags.HasFlag(CriFieldFlags.RowStorage))
            {
                field.Offset = offset;

                switch (field.Flags & CriFieldFlags.TypeMask)
                {
                    case CriFieldFlags.Byte:
                    case CriFieldFlags.SByte:
                        offset += 1;
                        break;
                    case CriFieldFlags.Int16:
                    case CriFieldFlags.UInt16:
                        offset += 2;
                        break;
                    case CriFieldFlags.Int32:
                    case CriFieldFlags.UInt32:
                    case CriFieldFlags.Single:
                    case CriFieldFlags.String:
                        offset += 4;
                        break;
                    case CriFieldFlags.Int64:
                    case CriFieldFlags.UInt64:
                    case CriFieldFlags.Double:
                    case CriFieldFlags.Data:
                        offset += 8;
                        break;
                }
            }

            _fields.Add(field);
        }
    }

    public string GetFieldName(int fieldIndex)
    {
        return _fields[fieldIndex].Name;
    }

    public Type GetFieldType(int fieldIndex)
    {
        return CriField.FieldTypes[(byte)_fields[fieldIndex].Flags & 0x0F];
    }

    public Type GetFieldType(string fieldName)
    {
        return CriField.FieldTypes[(byte)_fields[GetFieldIndex(fieldName)].Flags & 0x0F];
    }

    internal CriFieldFlags GetFieldFlag(string fieldName)
    {
        return _fields[GetFieldIndex(fieldName)].Flags;
    }

    internal CriFieldFlags GetFieldFlag(int fieldIndex)
    {
        return _fields[fieldIndex].Flags;
    }

    public object? GetFieldValue(int fieldIndex)
    {
        return _fields[fieldIndex].Value;
    }

    public object? GetFieldValue(string fieldName)
    {
        return _fields[GetFieldIndex(fieldName)].Value;
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
        return _fields.FindIndex(field => field.Name == fieldName);
    }

    public bool ContainsField(string fieldName)
    {
        return _fields.Exists(field => field.Name == fieldName);
    }

    private void GoToValue(int fieldIndex)
    {
        SourceStream.Seek(_headerPosition + _header.RowsPosition + _header.RowLength * CurrentRow + _fields[fieldIndex].Offset, SeekOrigin.Begin);
    }

    public bool Read()
    {
        if (CurrentRow + 1 >= _header.RowCount)
        {
            return false;
        }

        CurrentRow++;
        return true;
    }

    public bool MoveToRow(long rowIndex)
    {
        if (rowIndex >= _header.RowCount)
        {
            return false;
        }

        CurrentRow = rowIndex;
        return true;
    }

    public object?[] GetValueArray()
    {
        var values = new object?[_header.FieldCount];

        for (var i = 0; i < _header.FieldCount; i++)
        {
            if (_fields[i].Flags.HasFlag(CriFieldFlags.Data))
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
        for (var i = 0; i < _header.FieldCount; i++)
        {
            yield return GetValue(i);
        }
    }

    public object? GetValue(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= _fields.Count)
        {
            return null;
        }

        if (!_fields[fieldIndex].Flags.HasFlag(CriFieldFlags.RowStorage))
        {
            if (_fields[fieldIndex].Flags.HasFlag(CriFieldFlags.Data))
            {
                return new SubStream(SourceStream, 0, 0);
            }

            return _fields[fieldIndex].Value;
        }

        GoToValue(fieldIndex);
        return ReadValue(_fields[fieldIndex].Flags);
    }

    public object? GetValue(string fieldName)
    {
        return GetValue(GetFieldIndex(fieldName));
    }

    public T? GetValue<T>(int fieldIndex)
    {
        return (T?)GetValue(fieldIndex);
    }

    public T? GetValue<T>(string fieldName)
    {
        return (T?)GetValue(fieldName);
    }

    public byte? GetByte(int fieldIndex)
    {
        return (byte?)GetValue(fieldIndex);
    }

    public byte? GetByte(string fieldName)
    {
        return (byte?)GetValue(fieldName);
    }

    public sbyte? GetSByte(int fieldIndex)
    {
        return (sbyte?)GetValue(fieldIndex);
    }

    public sbyte? GetSByte(string fieldName)
    {
        return (sbyte?)GetValue(fieldName);
    }

    public ushort? GetUInt16(int fieldIndex)
    {
        return (ushort?)GetValue(fieldIndex);
    }

    public ushort? GetUInt16(string fieldName)
    {
        return (ushort?)GetValue(fieldName);
    }

    public short? GetInt16(int fieldIndex)
    {
        return (short?)GetValue(fieldIndex);
    }

    public short? GetInt16(string fieldName)
    {
        return (short?)GetValue(fieldName);
    }

    public uint? GetUInt32(int fieldIndex)
    {
        return (uint?)GetValue(fieldIndex);
    }

    public uint? GetUInt32(string fieldName)
    {
        return (uint?)GetValue(fieldName);
    }

    public int? GetInt32(int fieldIndex)
    {
        return (int?)GetValue(fieldIndex);
    }

    public int? GetInt32(string fieldName)
    {
        return (int?)GetValue(fieldName);
    }

    public ulong? GetUInt64(int fieldIndex)
    {
        return (ulong?)GetValue(fieldIndex);
    }

    public ulong? GetUInt64(string fieldName)
    {
        return (ulong?)GetValue(fieldName);
    }

    public long? GetInt64(int fieldIndex)
    {
        return (long?)GetValue(fieldIndex);
    }

    public long? GetInt64(string fieldName)
    {
        return (long?)GetValue(fieldName);
    }

    public float? GetSingle(int fieldIndex)
    {
        return (float?)GetValue(fieldIndex);
    }

    public float? GetSingle(string fieldName)
    {
        return (float?)GetValue(fieldName);
    }

    public double? GetDouble(int fieldIndex)
    {
        return (double?)GetValue(fieldIndex);
    }

    public double? GetDouble(string fieldName)
    {
        return (double?)GetValue(fieldName);
    }

    public string? GetString(int fieldIndex)
    {
        return (string?)GetValue(fieldIndex);
    }

    public string? GetString(string fieldName)
    {
        return (string?)GetValue(fieldName);
    }

    public SubStream? GetSubStream(int fieldIndex)
    {
        return (SubStream?)GetValue(fieldIndex);
    }

    public SubStream? GetSubStream(string fieldName)
    {
        return (SubStream?)GetValue(fieldName);
    }

    public byte[]? GetData(int fieldIndex)
    {
        using var ss = GetSubStream(fieldIndex);
        return ss?.ToArray();
    }

    public byte[]? GetData(string fieldName)
    {
        return GetData(GetFieldIndex(fieldName));
    }

    public CriTableReader GetTableReader(string fieldName)
    {
        var stream = GetSubStream(fieldName) ?? throw new InvalidOperationException($"Field '{fieldName}' does not contain a valid substream.");
        return new CriTableReader(stream, false);
    }

    public CriTableReader GetTableReader(int fieldIndex)
    {
        var stream = GetSubStream(fieldIndex) ?? throw new InvalidOperationException($"Field at index {fieldIndex} does not contain a valid substream.");
        return new CriTableReader(stream, false);
    }

    public uint GetLength(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= _fields.Count)
        {
            return 0;
        }

        if (!_fields[fieldIndex].Flags.HasFlag(CriFieldFlags.RowStorage))
        {
            return _fields[fieldIndex].Length;
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
        if (fieldIndex < 0 || fieldIndex >= _fields.Count)
        {
            return 0;
        }

        if (!_fields[fieldIndex].Flags.HasFlag(CriFieldFlags.RowStorage))
        {
            return _fields[fieldIndex].Position;
        }

        GoToValue(fieldIndex);
        return (uint)(_headerPosition + _header.DataPoolPosition + DataStream.ReadUInt32Be(SourceStream));
    }

    public uint? GetPosition(string fieldName)
    {
        return GetPosition(GetFieldIndex(fieldName));
    }

    public bool? GetBoolean(int fieldIndex)
    {
        return (byte?)GetValue(fieldIndex) > 0;
    }

    public bool? GetBoolean(string fieldName)
    {
        return (byte?)GetValue(fieldName) > 0;
    }

    public Guid? GetGuid(int fieldIndex)
    {
        return (Guid?)GetValue(fieldIndex);
    }

    public Guid? GetGuid(string fieldName)
    {
        return (Guid?)GetValue(fieldName);
    }

    private string ReadString()
    {
        var stringPosition = DataStream.ReadUInt32Be(SourceStream);
        var previousPosition = SourceStream.Position;

        SourceStream.Seek(_headerPosition + _header.StringPoolPosition + stringPosition, SeekOrigin.Begin);
        var readString = DataStream.ReadCString(SourceStream, EncodingType);

        SourceStream.Seek(previousPosition, SeekOrigin.Begin);

        if (readString == StringPool.AdxBlankString || readString == _header.TableName && stringPosition == 0)
        {
            return string.Empty;
        }

        return readString;
    }

    private object? ReadValue(CriFieldFlags fieldFlags)
    {
        switch (fieldFlags & CriFieldFlags.TypeMask)
        {
            case CriFieldFlags.Byte:
                return DataStream.ReadByte(SourceStream);
            case CriFieldFlags.SByte:
                return DataStream.ReadSByte(SourceStream);
            case CriFieldFlags.UInt16:
                return DataStream.ReadUInt16Be(SourceStream);
            case CriFieldFlags.Int16:
                return DataStream.ReadInt16Be(SourceStream);
            case CriFieldFlags.UInt32:
                return DataStream.ReadUInt32Be(SourceStream);
            case CriFieldFlags.Int32:
                return DataStream.ReadInt32Be(SourceStream);
            case CriFieldFlags.UInt64:
                return DataStream.ReadUInt64Be(SourceStream);
            case CriFieldFlags.Int64:
                return DataStream.ReadInt64Be(SourceStream);
            case CriFieldFlags.Single:
                return DataStream.ReadSingleBe(SourceStream);
            case CriFieldFlags.Double:
                return DataStream.ReadDoubleBe(SourceStream);
            case CriFieldFlags.String:
                return ReadString();
            case CriFieldFlags.Data:
                {
                    var position = DataStream.ReadUInt32Be(SourceStream);
                    var length = DataStream.ReadUInt32Be(SourceStream);

                    // Some ACB files have the length info set to zero for UTF table fields, so find the correct length
                    if (position > 0 && length == 0)
                    {
                        SourceStream.Seek(_headerPosition + _header.DataPoolPosition + position, SeekOrigin.Begin);

                        if (DataStream.ReadBytes(SourceStream, 4).SequenceEqual(CriTableHeader.SignatureBytes))
                        {
                            length = DataStream.ReadUInt32Be(SourceStream) + 8;
                        }
                    }

                    return new SubStream(SourceStream, _headerPosition + _header.DataPoolPosition + position, length);
                }
            case CriFieldFlags.Guid:
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
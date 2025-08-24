using SonicAudioLib.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SonicAudioLib.CriMw;

public sealed class CriTableWriter : IDisposable
{
    public enum Status
    {
        Begin,
        Start,
        FieldCollection,
        Row,
        Idle,
        End
    }

    private readonly List<CriTableField> _fields;

    private readonly CriTableWriterSettings _settings;
    private readonly StringPool _stringPool;
    private readonly DataPool _vldPool;
    private CriTableHeader _header;
    private uint _headerPosition;

    private CriTableWriter(Stream destination, CriTableWriterSettings settings)
    {
        DestinationStream = destination;
        this._settings = settings;

        _header = new CriTableHeader();
        _fields = [];
        _stringPool = new StringPool(settings.EncodingType);
        _vldPool = new DataPool(settings.Align);
    }

    public Status CurrentStatus { get; private set; } = Status.Begin;

    public Stream DestinationStream { get; }

    public void Dispose()
    {
        if (CurrentStatus != Status.End)
        {
            WriteEndTable();
        }

        _fields.Clear();
        _stringPool.Clear();
        _vldPool.Clear();

        if (!_settings.LeaveOpen)
        {
            DestinationStream.Close();
        }
    }

    public void WriteStartTable(string tableName = "(no name)")
    {
        if (CurrentStatus != Status.Begin)
        {
            throw new InvalidOperationException("Attempted to start table when the status wasn't Begin");
        }

        CurrentStatus = Status.Start;

        _headerPosition = (uint)DestinationStream.Position;

        if (_settings.PutBlankString)
        {
            _stringPool.Put(StringPool.AdxBlankString);
        }

        _header.TableNamePosition = (uint)_stringPool.Put(tableName);

        Span<byte> buffer = stackalloc byte[32];
        DestinationStream.Write(buffer);
    }

    public void WriteEndTable()
    {
        if (CurrentStatus == Status.FieldCollection)
        {
            WriteEndFieldCollection();
        }

        if (CurrentStatus == Status.Row)
        {
            WriteEndRow();
        }

        CurrentStatus = Status.End;

        DestinationStream.Seek(_headerPosition + _header.RowsPosition + _header.RowLength * _header.RowCount, SeekOrigin.Begin);

        _stringPool.Write(DestinationStream);
        _header.StringPoolPosition = (uint)_stringPool.Position - _headerPosition;

        DataStream.Pad(DestinationStream, _vldPool.Align);

        _vldPool.Write(DestinationStream);
        _header.DataPoolPosition = (uint)_vldPool.Position - _headerPosition;

        DataStream.Pad(DestinationStream, _vldPool.Align);

        var previousPosition = DestinationStream.Position;

        _header.Length = (uint)DestinationStream.Position - _headerPosition;

        if (Equals(_settings.EncodingType, Encoding.GetEncoding("shift-jis")))
        {
            _header.EncodingType = CriTableHeader.EncodingTypeShiftJis;
        }

        else if (Equals(_settings.EncodingType, Encoding.UTF8))
        {
            _header.EncodingType = CriTableHeader.EncodingTypeUtf8;
        }

        DestinationStream.Seek(_headerPosition, SeekOrigin.Begin);

        DestinationStream.Write(CriTableHeader.SignatureBytes, 0, 4);
        DataStream.WriteUInt32Be(DestinationStream, _header.Length - 8);
        DataStream.WriteByte(DestinationStream, _header.UnknownByte);
        DataStream.WriteByte(DestinationStream, _header.EncodingType);
        DataStream.WriteUInt16Be(DestinationStream, (ushort)(_header.RowsPosition - 8));
        DataStream.WriteUInt32Be(DestinationStream, _header.StringPoolPosition - 8);
        DataStream.WriteUInt32Be(DestinationStream, _header.DataPoolPosition - 8);
        DataStream.WriteUInt32Be(DestinationStream, _header.TableNamePosition);
        DataStream.WriteUInt16Be(DestinationStream, _header.FieldCount);
        DataStream.WriteUInt16Be(DestinationStream, _header.RowLength);
        DataStream.WriteUInt32Be(DestinationStream, _header.RowCount);

        if (_settings.EnableMask)
        {
            DestinationStream.Seek(_headerPosition, SeekOrigin.Begin);
            CriTableMasker.Mask(DestinationStream, _header.Length, _settings.MaskXor, _settings.MaskXorMultiplier);
        }

        DestinationStream.Seek(previousPosition, SeekOrigin.Begin);
    }

    public void WriteStartFieldCollection()
    {
        if (CurrentStatus != Status.Start)
        {
            throw new InvalidOperationException("Attempted to start field collection when the status wasn't Start");
        }

        CurrentStatus = Status.FieldCollection;
    }

    public void WriteField(string fieldName, Type fieldType, object? defaultValue)
    {
        if (CurrentStatus != Status.FieldCollection)
        {
            WriteStartFieldCollection();
        }

        var fieldFlag = (CriFieldFlags)Array.IndexOf(CriField.FieldTypes, fieldType);

        if (!string.IsNullOrEmpty(fieldName))
        {
            fieldFlag |= CriFieldFlags.Name;
        }

        if (defaultValue != null)
        {
            fieldFlag |= CriFieldFlags.DefaultValue;
        }

        var field = new CriTableField
        {
            Flags = fieldFlag,
            Name = fieldName,
            Value = defaultValue
        };

        DataStream.WriteByte(DestinationStream, (byte)field.Flags);

        if (!string.IsNullOrEmpty(fieldName))
        {
            WriteString(field.Name);
        }

        if (defaultValue != null)
        {
            WriteValue(defaultValue);
        }

        _fields.Add(field);
        _header.FieldCount++;
    }

    public void WriteField(string fieldName, Type fieldType)
    {
        if (CurrentStatus != Status.FieldCollection)
        {
            WriteStartFieldCollection();
        }

        var fieldFlag = (CriFieldFlags)Array.IndexOf(CriField.FieldTypes, fieldType) | CriFieldFlags.RowStorage;

        if (!string.IsNullOrEmpty(fieldName))
        {
            fieldFlag |= CriFieldFlags.Name;
        }

        var field = new CriTableField
        {
            Flags = fieldFlag,
            Name = fieldName
        };

        DataStream.WriteByte(DestinationStream, (byte)field.Flags);

        if (!string.IsNullOrEmpty(fieldName))
        {
            WriteString(field.Name);
        }

        field.Offset = _header.RowLength;
        switch (field.Flags & CriFieldFlags.TypeMask)
        {
            case CriFieldFlags.Byte:
            case CriFieldFlags.SByte:
                _header.RowLength += 1;
                break;
            case CriFieldFlags.Int16:
            case CriFieldFlags.UInt16:
                _header.RowLength += 2;
                break;
            case CriFieldFlags.Int32:
            case CriFieldFlags.UInt32:
            case CriFieldFlags.Single:
            case CriFieldFlags.String:
                _header.RowLength += 4;
                break;
            case CriFieldFlags.Int64:
            case CriFieldFlags.UInt64:
            case CriFieldFlags.Double:
            case CriFieldFlags.Data:
                _header.RowLength += 8;
                break;
        }

        _fields.Add(field);
        _header.FieldCount++;
    }

    public void WriteField(CriField criField)
    {
        WriteField(criField.FieldName, criField.FieldType);
    }

    public void WriteEndFieldCollection()
    {
        if (CurrentStatus != Status.FieldCollection)
        {
            throw new InvalidOperationException("Attempted to end field collection when the status wasn't FieldCollection");
        }

        CurrentStatus = Status.Idle;

        _header.RowsPosition = (ushort)(DestinationStream.Position - _headerPosition);
    }

    public void WriteStartRow()
    {
        if (CurrentStatus == Status.FieldCollection)
        {
            WriteEndFieldCollection();
        }

        if (CurrentStatus != Status.Idle)
        {
            throw new InvalidOperationException("Attempted to start row when the status wasn't Idle");
        }

        CurrentStatus = Status.Row;

        _header.RowCount++;

        DestinationStream.Seek(_headerPosition + _header.RowsPosition + _header.RowCount * _header.RowLength, SeekOrigin.Begin);
        var buffer = new byte[_header.RowLength];
        DestinationStream.Write(buffer);
    }

    public void WriteValue(int fieldIndex, object? rowValue)
    {
        if (fieldIndex >= _fields.Count || fieldIndex < 0 || !_fields[fieldIndex].Flags.HasFlag(CriFieldFlags.RowStorage) || rowValue == null)
        {
            return;
        }

        GoToValue(fieldIndex);
        WriteValue(rowValue);
    }

    public void WriteValue(string fieldName, object rowValue)
    {
        WriteValue(_fields.FindIndex(field => field.Name == fieldName), rowValue);
    }

    private void GoToValue(int fieldIndex)
    {
        DestinationStream.Seek(_headerPosition + _header.RowsPosition + _header.RowLength * (_header.RowCount - 1) + _fields[fieldIndex].Offset, SeekOrigin.Begin);
    }

    public void WriteEndRow()
    {
        if (CurrentStatus != Status.Row)
        {
            throw new InvalidOperationException("Attempted to end row when the status wasn't Row");
        }

        CurrentStatus = Status.Idle;
    }

    public void WriteRow(bool close, params object?[] rowValues)
    {
        WriteStartRow();

        for (var i = 0; i < Math.Min(rowValues.Length, _fields.Count); i++)
        {
            WriteValue(i, rowValues[i]);
        }

        if (close)
        {
            WriteEndRow();
        }
    }

    private void WriteString(string value)
    {
        if (_settings.RemoveDuplicateStrings && _stringPool.ContainsString(value))
        {
            DataStream.WriteUInt32Be(DestinationStream, (uint)_stringPool.GetStringPosition(value));
        }

        else
        {
            DataStream.WriteUInt32Be(DestinationStream, (uint)_stringPool.Put(value));
        }
    }

    private void WriteValue(object val)
    {
        switch (val)
        {
            case byte value:
                DataStream.WriteByte(DestinationStream, value);
                break;

            case sbyte value:
                DataStream.WriteSByte(DestinationStream, value);
                break;

            case ushort value:
                DataStream.WriteUInt16Be(DestinationStream, value);
                break;

            case short value:
                DataStream.WriteInt16Be(DestinationStream, value);
                break;

            case uint value:
                DataStream.WriteUInt32Be(DestinationStream, value);
                break;

            case int value:
                DataStream.WriteInt32Be(DestinationStream, value);
                break;

            case ulong value:
                DataStream.WriteUInt64Be(DestinationStream, value);
                break;

            case long value:
                DataStream.WriteInt64Be(DestinationStream, value);
                break;

            case float value:
                DataStream.WriteSingleBe(DestinationStream, value);
                break;

            case double value:
                DataStream.WriteDoubleBe(DestinationStream, value);
                break;

            case string value:
                WriteString(value);
                break;

            case byte[] value:
                DataStream.WriteUInt32Be(DestinationStream, (uint)_vldPool.Put(value));
                DataStream.WriteUInt32Be(DestinationStream, (uint)value.Length);
                break;

            case Guid value:
                DestinationStream.Write(value.ToByteArray(), 0, 16);
                break;

            case Stream value:
                DataStream.WriteUInt32Be(DestinationStream, (uint)_vldPool.Put(value));
                DataStream.WriteUInt32Be(DestinationStream, (uint)value.Length);
                break;

            case FileInfo value:
                DataStream.WriteUInt32Be(DestinationStream, (uint)_vldPool.Put(value));
                DataStream.WriteUInt32Be(DestinationStream, (uint)value.Length);
                break;
        }
    }

    public static CriTableWriter Create(string destinationFileName)
    {
        return Create(destinationFileName, new CriTableWriterSettings());
    }

    public static CriTableWriter Create(string destinationFileName, CriTableWriterSettings settings)
    {
        Stream destination = File.Create(destinationFileName);
        return new CriTableWriter(destination, settings);
    }

    public static CriTableWriter Create(Stream destination)
    {
        return new CriTableWriter(destination, new CriTableWriterSettings());
    }

    public static CriTableWriter Create(Stream destination, CriTableWriterSettings settings)
    {
        return new CriTableWriter(destination, settings);
    }
}

public class CriTableWriterSettings
{
    public uint Align
    {
        get;

        set
        {
            if (value <= 0)
            {
                value = 1;
            }

            field = value;
        }
    } = 1;

    public bool PutBlankString { get; set; } = true;

    public bool LeaveOpen { get; set; }

    public Encoding EncodingType
    {
        get;

        set
        {
            if (!Equals(value, Encoding.UTF8) || !Equals(value, Encoding.GetEncoding("shift-jis")))
            {
                return;
            }

            field = value;
        }
    } = Encoding.GetEncoding("shift-jis");

    public bool RemoveDuplicateStrings { get; set; } = true;

    public bool EnableMask { get; set; }

    public uint MaskXor { get; set; }
    public uint MaskXorMultiplier { get; set; }

    public static CriTableWriterSettings AdxSettings => new()
    {
        Align = 8,
        PutBlankString = true,
        RemoveDuplicateStrings = true
    };

    public static CriTableWriterSettings Adx2Settings => new()
    {
        Align = 32,
        PutBlankString = false,
        RemoveDuplicateStrings = false
    };
}
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

    private readonly List<CriTableField> fields;

    private readonly CriTableWriterSettings settings;
    private readonly StringPool stringPool;
    private readonly DataPool vldPool;
    private CriTableHeader header;
    private uint headerPosition;

    private CriTableWriter(Stream destination, CriTableWriterSettings settings)
    {
        DestinationStream = destination;
        this.settings = settings;

        header = new CriTableHeader();
        fields = [];
        stringPool = new StringPool(settings.EncodingType);
        vldPool = new DataPool(settings.Align);
    }

    public Status CurrentStatus { get; private set; } = Status.Begin;

    public Stream DestinationStream { get; }

    public void Dispose()
    {
        if (CurrentStatus != Status.End)
        {
            WriteEndTable();
        }

        fields.Clear();
        stringPool.Clear();
        vldPool.Clear();

        if (!settings.LeaveOpen)
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

        headerPosition = (uint)DestinationStream.Position;

        if (settings.PutBlankString)
        {
            stringPool.Put(StringPool.AdxBlankString);
        }

        header.TableNamePosition = (uint)stringPool.Put(tableName);

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

        DestinationStream.Seek(headerPosition + header.RowsPosition + header.RowLength * header.RowCount, SeekOrigin.Begin);

        stringPool.Write(DestinationStream);
        header.StringPoolPosition = (uint)stringPool.Position - headerPosition;

        DataStream.Pad(DestinationStream, vldPool.Align);

        vldPool.Write(DestinationStream);
        header.DataPoolPosition = (uint)vldPool.Position - headerPosition;

        DataStream.Pad(DestinationStream, vldPool.Align);

        var previousPosition = DestinationStream.Position;

        header.Length = (uint)DestinationStream.Position - headerPosition;

        if (Equals(settings.EncodingType, Encoding.GetEncoding("shift-jis")))
        {
            header.EncodingType = CriTableHeader.EncodingTypeShiftJis;
        }

        else if (Equals(settings.EncodingType, Encoding.UTF8))
        {
            header.EncodingType = CriTableHeader.EncodingTypeUtf8;
        }

        DestinationStream.Seek(headerPosition, SeekOrigin.Begin);

        DestinationStream.Write(CriTableHeader.SignatureBytes, 0, 4);
        DataStream.WriteUInt32Be(DestinationStream, header.Length - 8);
        DataStream.WriteByte(DestinationStream, header.UnknownByte);
        DataStream.WriteByte(DestinationStream, header.EncodingType);
        DataStream.WriteUInt16Be(DestinationStream, (ushort)(header.RowsPosition - 8));
        DataStream.WriteUInt32Be(DestinationStream, header.StringPoolPosition - 8);
        DataStream.WriteUInt32Be(DestinationStream, header.DataPoolPosition - 8);
        DataStream.WriteUInt32Be(DestinationStream, header.TableNamePosition);
        DataStream.WriteUInt16Be(DestinationStream, header.FieldCount);
        DataStream.WriteUInt16Be(DestinationStream, header.RowLength);
        DataStream.WriteUInt32Be(DestinationStream, header.RowCount);

        if (settings.EnableMask)
        {
            DestinationStream.Seek(headerPosition, SeekOrigin.Begin);
            CriTableMasker.Mask(DestinationStream, header.Length, settings.MaskXor, settings.MaskXorMultiplier);
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

    public void WriteField(string fieldName, Type fieldType, object defaultValue)
    {
        if (CurrentStatus != Status.FieldCollection)
        {
            WriteStartFieldCollection();
        }

        var fieldFlag = (CriFieldFlag)Array.IndexOf(CriField.FieldTypes, fieldType);

        if (!string.IsNullOrEmpty(fieldName))
        {
            fieldFlag |= CriFieldFlag.Name;
        }

        if (defaultValue != null)
        {
            fieldFlag |= CriFieldFlag.DefaultValue;
        }

        var field = new CriTableField
        {
            Flag = fieldFlag,
            Name = fieldName,
            Value = defaultValue
        };

        DataStream.WriteByte(DestinationStream, (byte)field.Flag);

        if (!string.IsNullOrEmpty(fieldName))
        {
            WriteString(field.Name);
        }

        if (defaultValue != null)
        {
            WriteValue(defaultValue);
        }

        fields.Add(field);
        header.FieldCount++;
    }

    public void WriteField(string fieldName, Type fieldType)
    {
        if (CurrentStatus != Status.FieldCollection)
        {
            WriteStartFieldCollection();
        }

        var fieldFlag = (CriFieldFlag)Array.IndexOf(CriField.FieldTypes, fieldType) | CriFieldFlag.RowStorage;

        if (!string.IsNullOrEmpty(fieldName))
        {
            fieldFlag |= CriFieldFlag.Name;
        }

        var field = new CriTableField
        {
            Flag = fieldFlag,
            Name = fieldName
        };

        DataStream.WriteByte(DestinationStream, (byte)field.Flag);

        if (!string.IsNullOrEmpty(fieldName))
        {
            WriteString(field.Name);
        }

        field.Offset = header.RowLength;
        switch (field.Flag & CriFieldFlag.TypeMask)
        {
            case CriFieldFlag.Byte:
            case CriFieldFlag.SByte:
                header.RowLength += 1;
                break;
            case CriFieldFlag.Int16:
            case CriFieldFlag.UInt16:
                header.RowLength += 2;
                break;
            case CriFieldFlag.Int32:
            case CriFieldFlag.UInt32:
            case CriFieldFlag.Single:
            case CriFieldFlag.String:
                header.RowLength += 4;
                break;
            case CriFieldFlag.Int64:
            case CriFieldFlag.UInt64:
            case CriFieldFlag.Double:
            case CriFieldFlag.Data:
                header.RowLength += 8;
                break;
        }

        fields.Add(field);
        header.FieldCount++;
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

        header.RowsPosition = (ushort)(DestinationStream.Position - headerPosition);
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

        header.RowCount++;

        DestinationStream.Seek(headerPosition + header.RowsPosition + header.RowCount * header.RowLength, SeekOrigin.Begin);
        var buffer = new byte[header.RowLength];
        DestinationStream.Write(buffer);
    }

    public void WriteValue(int fieldIndex, object rowValue)
    {
        if (fieldIndex >= fields.Count || fieldIndex < 0 || !fields[fieldIndex].Flag.HasFlag(CriFieldFlag.RowStorage) || rowValue == null)
        {
            return;
        }

        GoToValue(fieldIndex);
        WriteValue(rowValue);
    }

    public void WriteValue(string fieldName, object rowValue)
    {
        WriteValue(fields.FindIndex(field => field.Name == fieldName), rowValue);
    }

    private void GoToValue(int fieldIndex)
    {
        DestinationStream.Seek(headerPosition + header.RowsPosition + header.RowLength * (header.RowCount - 1) + fields[fieldIndex].Offset, SeekOrigin.Begin);
    }

    public void WriteEndRow()
    {
        if (CurrentStatus != Status.Row)
        {
            throw new InvalidOperationException("Attempted to end row when the status wasn't Row");
        }

        CurrentStatus = Status.Idle;
    }

    public void WriteRow(bool close, params object[] rowValues)
    {
        WriteStartRow();

        for (var i = 0; i < Math.Min(rowValues.Length, fields.Count); i++)
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
        if (settings.RemoveDuplicateStrings && stringPool.ContainsString(value))
        {
            DataStream.WriteUInt32Be(DestinationStream, (uint)stringPool.GetStringPosition(value));
        }

        else
        {
            DataStream.WriteUInt32Be(DestinationStream, (uint)stringPool.Put(value));
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
                DataStream.WriteUInt32Be(DestinationStream, (uint)vldPool.Put(value));
                DataStream.WriteUInt32Be(DestinationStream, (uint)value.Length);
                break;

            case Guid value:
                DestinationStream.Write(value.ToByteArray(), 0, 16);
                break;

            case Stream value:
                DataStream.WriteUInt32Be(DestinationStream, (uint)vldPool.Put(value));
                DataStream.WriteUInt32Be(DestinationStream, (uint)value.Length);
                break;

            case FileInfo value:
                DataStream.WriteUInt32Be(DestinationStream, (uint)vldPool.Put(value));
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
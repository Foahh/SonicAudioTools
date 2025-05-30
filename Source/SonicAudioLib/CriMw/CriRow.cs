using System.Collections;
using System.Collections.Generic;

namespace SonicAudioLib.CriMw;

internal class CriRowRecord
{
    public required CriField Field { get; set; }
    public required object? Value { get; set; }
}

public class CriRow : IEnumerable
{
    internal CriRow(CriTable parent)
    {
        Parent = parent;
    }

    public object? this[CriField criField]
    {
        get
        {
            return this[Records.FindIndex(record => record.Field == criField)];
        }

        set
        {
            this[Records.FindIndex(record => record.Field == criField)] = value;
        }
    }

    public object? this[int index]
    {
        get
        {
            if (index < 0 || index >= Records.Count)
            {
                return null;
            }

            return Records[index].Value;
        }

        set
        {
            if (index < 0 || index >= Records.Count)
            {
                return;
            }

            Records[index].Value = value;
        }
    }

    public object? this[string name]
    {
        get
        {
            return this[Records.FindIndex(record => record.Field.FieldName == name)];
        }

        set
        {
            this[Records.FindIndex(record => record.Field.FieldName == name)] = value;
        }
    }

    public CriTable Parent { get; internal set; }

    internal List<CriRowRecord> Records { get; } = [];

    public int FieldCount => Records.Count;

    public IEnumerator GetEnumerator()
    {
        foreach (var record in Records)
        {
            yield return record.Value;
        }
    }

    public T? GetValue<T>(CriField criField)
    {
        return (T?)this[criField];
    }

    public T? GetValue<T>(string fieldName)
    {
        return (T?)this[fieldName];
    }

    public T? GetValue<T>(int fieldIndex)
    {
        return (T?)this[fieldIndex];
    }

    public object?[] GetValueArray()
    {
        var values = new object?[Records.Count];

        for (var i = 0; i < Records.Count; i++)
        {
            values[i] = Records[i].Value;
        }

        return values;
    }
}
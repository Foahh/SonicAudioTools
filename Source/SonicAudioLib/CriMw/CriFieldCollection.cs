using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SonicAudioLib.CriMw;

public class CriFieldCollection(CriTable parent) : IEnumerable<CriField>
{
    private readonly List<CriField> fields = [];

    public CriField this[int index] => fields[index];

    public CriField this[string name]
    {
        get
        {
            return fields.FirstOrDefault(field => field.FieldName == name);
        }
    }

    public int Count => fields.Count;

    public CriTable Parent { get; internal set; } = parent;

    public IEnumerator<CriField> GetEnumerator()
    {
        return ((IEnumerable<CriField>)fields).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<CriField>)fields).GetEnumerator();
    }

    public void Add(CriField criField)
    {
        criField.Parent = Parent;
        fields.Add(criField);
    }

    public CriField Add(string name, Type type)
    {
        var criField = new CriField(name, type);
        Add(criField);

        return criField;
    }

    public CriField Add(string name, Type type, object defaultValue)
    {
        var criField = new CriField(name, type, defaultValue);
        Add(criField);

        return criField;
    }

    public void Insert(int index, CriField criField)
    {
        if (index >= fields.Count || index < 0)
        {
            fields.Add(criField);
        }

        else
        {
            fields.Insert(index, criField);
        }
    }

    public void Remove(CriField criField)
    {
        fields.Remove(criField);

        // Update the rows
        foreach (var criRow in Parent.Rows)
        {
            criRow.Records.RemoveAll(record => record.Field == criField);
        }
    }

    public void RemoveAt(int index)
    {
        Remove(fields[index]);
    }

    internal void Clear()
    {
        fields.Clear();
    }
}
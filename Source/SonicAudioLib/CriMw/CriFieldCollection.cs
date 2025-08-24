using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SonicAudioLib.CriMw;

public class CriFieldCollection(CriTable parent) : IEnumerable<CriField>
{
    private readonly List<CriField> _fields = [];

    public CriField this[int index] => _fields[index];

    public CriField? this[string name]
    {
        get
        {
            return _fields.FirstOrDefault(field => field.FieldName == name);
        }
    }

    public int Count => _fields.Count;

    public CriTable? Parent { get; internal set; } = parent;

    public IEnumerator<CriField> GetEnumerator()
    {
        return ((IEnumerable<CriField>)_fields).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<CriField>)_fields).GetEnumerator();
    }

    public void Add(CriField criField)
    {
        criField.Parent = Parent;
        _fields.Add(criField);
    }

    public CriField Add(string name, Type type)
    {
        var criField = new CriField(name, type);
        Add(criField);

        return criField;
    }

    public CriField Add(string name, Type type, object? defaultValue)
    {
        var criField = new CriField(name, type, defaultValue);
        Add(criField);

        return criField;
    }

    public void Insert(int index, CriField criField)
    {
        if (index >= _fields.Count || index < 0)
        {
            _fields.Add(criField);
        }

        else
        {
            _fields.Insert(index, criField);
        }
    }

    public void Remove(CriField criField)
    {
        _fields.Remove(criField);

        if (Parent is null)
        {
            return;
        }
        
        foreach (var criRow in Parent.Rows)
        {
            criRow.Records.RemoveAll(record => record.Field == criField);
        }
    }

    public void RemoveAt(int index)
    {
        Remove(_fields[index]);
    }

    internal void Clear()
    {
        _fields.Clear();
    }
}
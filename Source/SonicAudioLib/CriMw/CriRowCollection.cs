using System;
using System.Collections;
using System.Collections.Generic;

namespace SonicAudioLib.CriMw;

public class CriRowCollection(CriTable parent) : IEnumerable<CriRow>
{
    private readonly List<CriRow> _rows = [];

    public CriRow this[int index] => _rows[index];

    public int Count => _rows.Count;

    public CriTable Parent { get; internal set; } = parent;

    public IEnumerator<CriRow> GetEnumerator()
    {
        return ((IEnumerable<CriRow>)_rows).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<CriRow>)_rows).GetEnumerator();
    }

    public void Add(CriRow criRow)
    {
        criRow.Parent = Parent;
        _rows.Add(criRow);
    }

    public CriRow Add(params object?[] objs)
    {
        var criRow = Parent.NewRow();

        var objects = new object[criRow.FieldCount];
        Array.Copy(objs, objects, Math.Min(objs.Length, objects.Length));

        for (var i = 0; i < criRow.FieldCount; i++)
        {
            criRow[i] = objects[i];
        }

        Add(criRow);
        return criRow;
    }

    internal void Clear()
    {
        _rows.Clear();
    }
}
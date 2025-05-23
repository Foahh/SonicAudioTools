using System;
using System.Collections;
using System.Collections.Generic;

namespace SonicAudioLib.CriMw;

public class CriRowCollection(CriTable parent) : IEnumerable<CriRow>
{
    private readonly List<CriRow> rows = new();

    public CriRow this[int index] => rows[index];

    public int Count => rows.Count;

    public CriTable Parent { get; internal set; } = parent;

    public IEnumerator<CriRow> GetEnumerator()
    {
        return ((IEnumerable<CriRow>)rows).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<CriRow>)rows).GetEnumerator();
    }

    public void Add(CriRow criRow)
    {
        criRow.Parent = Parent;
        rows.Add(criRow);
    }

    public CriRow Add(params object[] objs)
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
        rows.Clear();
    }
}
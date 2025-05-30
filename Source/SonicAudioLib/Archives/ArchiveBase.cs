using SonicAudioLib.FileBases;
using SonicAudioLib.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace SonicAudioLib.Archives;

public abstract class EntryBase
{
    public virtual long Position { get; set; }

    public virtual long Length
    {
        get => Stream?.Length ?? field;
        set;
    }

    public virtual Stream Stream { get; set; }

    public virtual Stream Open(Stream source)
    {
        return new SubStream(source, Position, Length);
    }
}

public abstract class ArchiveBase<T> : FileBase, IList<T>
{
    protected readonly List<T> Entries = [];

    public virtual int Count => Entries.Count;

    public virtual bool IsReadOnly => false;

    public virtual T this[int index]
    {
        get => Entries[index];

        set => Entries[index] = value;
    }

    public virtual int IndexOf(T item)
    {
        return Entries.IndexOf(item);
    }

    public virtual void Insert(int index, T item)
    {
        Entries.Insert(index, item);
    }

    public virtual void RemoveAt(int index)
    {
        Entries.RemoveAt(index);
    }

    public virtual void Add(T item)
    {
        Entries.Add(item);
    }

    public virtual void Clear()
    {
        Entries.Clear();
    }

    public virtual bool Contains(T item)
    {
        return Entries.Contains(item);
    }

    public virtual void CopyTo(T[] array, int arrayIndex)
    {
        Entries.CopyTo(array, arrayIndex);
    }

    public virtual bool Remove(T item)
    {
        return Entries.Remove(item);
    }

    public virtual IEnumerator<T> GetEnumerator()
    {
        return Entries.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Entries.GetEnumerator();
    }

    public IProgress<double> Progress { get; set; }
}
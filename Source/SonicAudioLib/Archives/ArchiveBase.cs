using SonicAudioLib.FileBases;
using SonicAudioLib.IO;
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

    public event ProgressChanged ProgressChanged;

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

    protected virtual void OnProgressChanged(object sender, ProgressChangedEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

#if DEBUG
    public virtual void Print()
    {
        Type archiveType = GetType();
        Console.WriteLine("{0}:", archiveType.Name);

        foreach (PropertyInfo property in archiveType.GetProperties().Where(p => p.GetIndexParameters().Length == 0))
        {
            Console.WriteLine(" {0}: {1}", property.Name, property.GetValue(this));
        }

        foreach (T entry in entries)
        {
            Type entryType = entry.GetType();

            Console.WriteLine("{0}:", entryType.Name);
            foreach (PropertyInfo property in entryType.GetProperties().Where(p => p.GetIndexParameters().Length == 0))
            {
                Console.WriteLine(" {0}: {1}", property.Name, property.GetValue(entry));
            }
        }
    }
#endif
}
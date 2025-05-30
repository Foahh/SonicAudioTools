using System;
using System.IO;

namespace SonicAudioLib.FileBases;

public abstract class FileBase
{
    protected int BufferSize = 4096;

    public abstract void Read(Stream source);
    public abstract void Write(Stream destination);

    public virtual void Load(string sourceFileName, int bufferSize)
    {
        using (Stream source = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize))
        {
            Read(source);
        }

        BufferSize = bufferSize;
    }

    public virtual void Load(string sourceFileName)
    {
        Load(sourceFileName, 4096);
    }

    public virtual void Load(byte[]? sourceByteArray)
    {
        using Stream source = new MemoryStream(sourceByteArray!);
        Read(source);
    }

    public virtual void Load(Stream sourceStream)
    {
        Read(sourceStream);
    }

    public virtual void Save(string destinationFileName)
    {
        Save(destinationFileName, 4096);
    }

    public virtual void Save(string destinationFileName, int bufferSize)
    {
        using (Stream destination = File.Create(destinationFileName, bufferSize))
        {
            Write(destination);
        }

        BufferSize = bufferSize;
    }

    public virtual byte[] Save()
    {
        using var destination = new MemoryStream();
        Write(destination);
        return destination.ToArray();
    }

    public virtual void Save(Stream destination)
    {
        Write(destination);
    }
}
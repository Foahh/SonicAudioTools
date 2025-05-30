using System;
using System.Collections;
using System.IO;

namespace SonicAudioLib.IO;

public class DataPool
{
    private readonly uint align = 1;
    private readonly long baseLength;
    private readonly ArrayList items = [];

    public DataPool(uint align, long baseLength)
    {
        this.align = align;

        this.baseLength = baseLength;
        Length = this.baseLength;
    }

    public DataPool(uint align)
    {
        this.align = align;
    }

    public DataPool()
    {
    }

    public long Position { get; private set; }

    public long Length { get; private set; }

    public long Align => align;

    public IProgress<double> Progress { get; init; }

    public long Put(byte[] data)
    {
        if (data == null || data.Length <= 0)
        {
            return 0;
        }

        Length = Helpers.Align(Length, align);

        var position = Length;
        Length += data.Length;
        items.Add(data);

        return position;
    }

    public long Put(Stream stream)
    {
        if (stream == null || stream.Length <= 0)
        {
            return 0;
        }

        Length = Helpers.Align(Length, align);

        var position = Length;
        Length += stream.Length;
        items.Add(stream);

        return position;
    }

    public long Put(FileInfo fileInfo)
    {
        if (fileInfo == null || fileInfo.Length <= 0)
        {
            return 0;
        }

        Length = Helpers.Align(Length, align);

        var position = Length;
        Length += fileInfo.Length;
        items.Add(fileInfo);

        return position;
    }

    public void Write(Stream destination)
    {
        Position = destination.Position;

        foreach (var item in items)
        {
            DataStream.Pad(destination, align);

            if (item is byte[] bytes) destination.Write(bytes);

            else if (item is Stream stream)
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(destination);
            }

            else if (item is FileInfo fileInfo)
            {
                using Stream source = fileInfo.OpenRead();
                source.CopyTo(destination);
            }

            Progress.Report((destination.Position - Position) / (double)(Length - baseLength) * 100.0);
        }
    }

    public void Clear()
    {
        items.Clear();
    }
}
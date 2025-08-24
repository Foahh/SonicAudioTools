using System;
using System.Collections;
using System.IO;

namespace SonicAudioLib.IO;

public class DataPool
{
    private readonly uint _align = 1;
    private readonly long _baseLength;
    private readonly ArrayList _items = [];

    public DataPool(uint align, long baseLength)
    {
        this._align = align;

        this._baseLength = baseLength;
        Length = this._baseLength;
    }

    public DataPool(uint align)
    {
        this._align = align;
    }

    public DataPool()
    {
    }

    public long Position { get; private set; }

    public long Length { get; private set; }

    public long Align => _align;

    public IProgress<double>? Progress { get; init; }

    public long Put(byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            return 0;
        }

        Length = Helpers.Align(Length, _align);

        var position = Length;
        Length += data.Length;
        _items.Add(data);

        return position;
    }

    public long Put(Stream? stream)
    {
        if (stream is not { Length: > 0 })
        {
            return 0;
        }

        Length = Helpers.Align(Length, _align);

        var position = Length;
        Length += stream.Length;
        _items.Add(stream);

        return position;
    }

    public long Put(FileInfo? fileInfo)
    {
        if (fileInfo is not { Length: > 0 })
        {
            return 0;
        }

        Length = Helpers.Align(Length, _align);

        var position = Length;
        Length += fileInfo.Length;
        _items.Add(fileInfo);

        return position;
    }

    public void Write(Stream destination)
    {
        Position = destination.Position;

        foreach (var item in _items)
        {
            DataStream.Pad(destination, _align);

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

            Progress?.Report((destination.Position - Position) / (double)(Length - _baseLength) * 100.0);
        }
    }

    public void Clear()
    {
        _items.Clear();
    }
}
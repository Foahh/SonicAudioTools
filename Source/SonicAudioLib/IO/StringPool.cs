using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonicAudioLib.IO;

public class StringPool
{
    public const string AdxBlankString = "<NULL>";
    private readonly Encoding _encoding = Encoding.Default;
    private readonly List<StringItem> _items = [];

    public StringPool(Encoding encoding)
    {
        this._encoding = encoding;
    }

    public StringPool()
    {
    }

    public long Position { get; private set; }

    public long Length { get; private set; }

    public long Put(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var position = Length;
        _items.Add(new StringItem { Value = value, Position = position });

        Length += _encoding.GetByteCount(value) + 1;
        return position;
    }

    public void Write(Stream destination)
    {
        Position = (uint)destination.Position;

        foreach (var item in _items)
        {
            DataStream.WriteCString(destination, item.Value, _encoding);
        }
    }

    public bool ContainsString(string value)
    {
        return _items.Any(item => item.Value == value);
    }

    public long GetStringPosition(string value)
    {
        return _items.First(item => item.Value == value).Position;
    }

    public void Clear()
    {
        _items.Clear();
    }

    private sealed class StringItem
    {
        public required string Value { get; set; }
        public long Position { get; set; }
    }
}
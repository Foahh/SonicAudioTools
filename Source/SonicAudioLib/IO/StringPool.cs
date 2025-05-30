using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SonicAudioLib.IO;

public class StringPool
{
    public const string AdxBlankString = "<NULL>";
    private readonly Encoding encoding = Encoding.Default;
    private readonly List<StringItem> items = [];

    public StringPool(Encoding encoding)
    {
        this.encoding = encoding;
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
        items.Add(new StringItem { Value = value, Position = position });

        Length += encoding.GetByteCount(value) + 1;
        return position;
    }

    public void Write(Stream destination)
    {
        Position = (uint)destination.Position;

        foreach (var item in items)
        {
            DataStream.WriteCString(destination, item.Value, encoding);
        }
    }

    public bool ContainsString(string value)
    {
        return items.Any(item => item.Value == value);
    }

    public long GetStringPosition(string value)
    {
        return items.First(item => item.Value == value).Position;
    }

    public void Clear()
    {
        items.Clear();
    }

    private sealed class StringItem
    {
        public required string Value { get; set; }
        public long Position { get; set; }
    }
}
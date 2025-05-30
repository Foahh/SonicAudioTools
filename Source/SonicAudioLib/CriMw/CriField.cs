using System;
using System.ComponentModel;

namespace SonicAudioLib.CriMw;

public class CriField
{
    public static readonly Type[] FieldTypes =
    [
        typeof(byte),
        typeof(sbyte),
        typeof(ushort),
        typeof(short),
        typeof(uint),
        typeof(int),
        typeof(ulong),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(string),
        typeof(byte[]),
        typeof(Guid)
    ];

    public static readonly object[] NullValues =
    [
        (byte)0,
        (sbyte)0,
        (ushort)0,
        (short)0,
        (uint)0,
        0,
        (ulong)0,
        (long)0,
        0f,
        0.0,
        string.Empty,
        Array.Empty<byte>(),
        Guid.Empty
    ];

    public CriField(string name, Type type)
    {
        FieldName = name;
        FieldType = type;
    }

    public CriField(string name, Type type, object? defaultValue)
    {
        FieldName = name;
        FieldType = type;
        DefaultValue = ConvertObject(defaultValue);
    }

    public int FieldTypeIndex => Array.IndexOf(FieldTypes, FieldType);

    public Type FieldType { get; }

    public object? DefaultValue
    {
        get;
        set => field = ConvertObject(value);
    }

    public string FieldName { get; }

    public CriTable? Parent { get; internal set; }

    public object? ConvertObject(object? obj)
    {
        if (obj == null)
        {
            return NullValues[FieldTypeIndex];
        }

        var typ = obj.GetType();

        if (typ == FieldType)
        {
            return obj;
        }

        var typeConverter = TypeDescriptor.GetConverter(FieldType);

        if (typeConverter.CanConvertFrom(typ))
        {
            return typeConverter.ConvertFrom(obj) ?? NullValues[FieldTypeIndex];
        }

        return DefaultValue;
    }
}
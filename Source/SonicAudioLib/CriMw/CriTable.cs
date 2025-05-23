using SonicAudioLib.FileBases;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SonicAudioLib.CriMw;

public class CriTable : FileXmlBase
{
    public CriTable()
    {
        Fields = new CriFieldCollection(this);
        Rows = new CriRowCollection(this);
        WriterSettings = new CriTableWriterSettings();
    }

    public CriTable(string tableName) : this()
    {
        TableName = tableName;
    }

    public CriFieldCollection Fields { get; }

    public CriRowCollection Rows { get; }

    public string TableName { get; set; } = "(no name)";

    public CriTableWriterSettings WriterSettings { get; set; }

    public void Clear()
    {
        Rows.Clear();
        Fields.Clear();
    }

    public CriRow NewRow()
    {
        var criRow = new CriRow(this);

        foreach (var criField in Fields)
        {
            criRow.Records.Add(new CriRowRecord { Field = criField, Value = criField.DefaultValue });
        }

        return criRow;
    }

    public override void Read(Stream source)
    {
        using var reader = CriTableReader.Create(source);
        TableName = reader.TableName;

        for (var i = 0; i < reader.NumberOfFields; i++)
        {
            Fields.Add(reader.GetFieldName(i), reader.GetFieldType(i), reader.GetFieldValue(i));
        }

        while (reader.Read())
        {
            Rows.Add(reader.GetValueArray());
        }
    }

    public override void Write(Stream destination)
    {
        using var writer = CriTableWriter.Create(destination, WriterSettings);
        writer.WriteStartTable(TableName);

        writer.WriteStartFieldCollection();
        foreach (var criField in Fields)
        {
            var useDefaultValue = false;
            object defaultValue = null;

            if (Rows.Count > 1)
            {
                useDefaultValue = true;
                defaultValue = Rows[0][criField];

                if (Rows.Any(row => !row[criField].Equals(defaultValue)))
                {
                    useDefaultValue = false;
                }
            }

            else if (Rows.Count == 0)
            {
                useDefaultValue = true;
            }

            if (useDefaultValue)
            {
                writer.WriteField(criField.FieldName, criField.FieldType, defaultValue);
            }

            else
            {
                writer.WriteField(criField.FieldName, criField.FieldType);
            }
        }
        writer.WriteEndFieldCollection();

        foreach (var criRow in Rows)
        {
            writer.WriteRow(true, criRow.GetValueArray());
        }

        writer.WriteEndTable();
    }

    public override void ReadXml(XmlReader reader)
    {
        var document = XDocument.Load(reader);

        foreach (var element in document.Root.Element(nameof(Fields)).Elements(nameof(CriField)))
        {
            Fields.Add(
                element.Element(nameof(CriField.FieldName)).Value,
                Type.GetType(element.Element(nameof(CriField.FieldType)).Value)
            );
        }

        foreach (var element in document.Root.Element(nameof(Rows)).Elements(nameof(CriRow)))
        {
            var row = NewRow();

            foreach (var record in row.Records)
            {
                if (record.Field.FieldType == typeof(byte[]))
                {
                    record.Value = Convert.FromBase64String(element.Element(record.Field.FieldName).Value);
                }

                else
                {
                    record.Value = Convert.ChangeType(element.Element(record.Field.FieldName).Value, record.Field.FieldType);
                }
            }

            Rows.Add(row);
        }
    }

    public override void WriteXml(XmlWriter writer)
    {
        var document = new XDocument(new XElement(nameof(CriTable)));
        document.Root.Add(new XElement(nameof(TableName), TableName));

        var fieldsElement = new XElement(nameof(Fields));

        foreach (var field in Fields)
        {
            var fieldElement = new XElement(nameof(CriField));

            fieldElement.Add(
                new XElement(nameof(field.FieldName), field.FieldName),
                new XElement(nameof(field.FieldType), field.FieldType.Name)
            );

            fieldsElement.Add(fieldElement);
        }

        document.Root.Add(fieldsElement);

        var rowsElement = new XElement(nameof(Rows));

        foreach (var row in Rows)
        {
            var rowElement = new XElement(nameof(CriRow));

            foreach (var record in row.Records)
            {
                if (record.Value is byte[] bytes)
                {
                    rowElement.Add(new XElement(record.Field.FieldName, Convert.ToBase64String(bytes)));
                }

                else
                {
                    rowElement.Add(new XElement(record.Field.FieldName, record.Value));
                }
            }

            rowsElement.Add(rowElement);
        }

        document.Root.Add(rowsElement);
        document.Save(writer);
    }
}
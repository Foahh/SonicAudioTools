using System.Xml;

namespace SonicAudioLib.FileBases;

// Because C# doesn't allow you to inherit
// more than 1 abstract class.
public abstract class FileXmlBase : FileBase
{
    public abstract void ReadXml(XmlReader reader);
    public abstract void WriteXml(XmlWriter writer);

    public virtual void LoadXml(string sourceFileName)
    {
        using var reader = XmlReader.Create(sourceFileName);
        ReadXml(reader);
    }

    public virtual void SaveXml(string destinationFileName)
    {
        var settings = new XmlWriterSettings();
        settings.Indent = true;

        using var writer = XmlWriter.Create(destinationFileName, settings);
        WriteXml(writer);
    }
}
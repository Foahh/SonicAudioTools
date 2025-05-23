using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SonicAudioLib.IO;

public class DataExtractor
{
    public enum LoggingType
    {
        Progress,
        Message
    }

    private readonly List<Item> items = [];

    public int BufferSize { get; set; } = 4096;
    public bool EnableThreading { get; set; } = true;
    public int MaxThreads { get; set; } = 4;

    public event ProgressChanged ProgressChanged;

    public void Add(object source, string destinationFileName, long position, long length)
    {
        items.Add(new Item { Source = source, DestinationFileName = destinationFileName, Position = position, Length = length });
    }

    public void Run()
    {
        var progress = 0.0;
        var factor = 100.0 / items.Count;

        var lockTarget = new object();

        Action<Item> action = item =>
        {
            if (ProgressChanged != null)
            {
                lock (lockTarget)
                {
                    progress += factor;
                    ProgressChanged(this, new ProgressChangedEventArgs(progress));
                }
            }

            var destinationFileName = new FileInfo(item.DestinationFileName);

            if (!destinationFileName.Directory.Exists)
            {
                destinationFileName.Directory.Create();
            }

            using var source =
                item.Source is string fileName ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize) :
                item.Source is byte[] byteArray ? new MemoryStream(byteArray) :
                item.Source is Stream stream ? stream :
                throw new ArgumentException("Unknown source in item", nameof(item.Source));
            using Stream destination = destinationFileName.Create();
            DataStream.CopyPartTo(source, destination, item.Position, item.Length, BufferSize);
        };

        if (EnableThreading)
        {
            Parallel.ForEach(items, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, action);
        }

        else
        {
            items.ForEach(action);
        }

        items.Clear();
    }

    private class Item
    {
        public object Source { get; set; }
        public string DestinationFileName { get; set; }
        public long Position { get; set; }
        public long Length { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SonicAudioLib.IO;

public class DataExtractor
{
    private readonly List<Item> _items = [];

    public int BufferSize { get; set; } = 4096;
    public bool EnableThreading { get; set; } = true;
    public int MaxThreads { get; set; } = 4;

    public IProgress<double>? Progress { get; set; }

    public void Add(object source, string destinationFileName, long position, long length)
    {
        _items.Add(new Item { Source = source, DestinationFileName = destinationFileName, Position = position, Length = length });
    }

    public void Run()
    {
        var progress = 0.0;
        var factor = 100.0 / _items.Count;

        Action<Item> action = item =>
        {
            if (Progress != null)
            {
                var newProgress = Interlocked.Exchange(ref progress, progress + factor);
                Progress.Report(Math.Round(newProgress, 2, MidpointRounding.AwayFromZero));
            }

            var destinationFileName = new FileInfo(item.DestinationFileName);
            if (destinationFileName.Directory is { Exists: false })
            {
                destinationFileName.Directory.Create();
            }

            using var source = item.Source switch
            {
                string fileName => new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize),
                byte[] byteArray => new MemoryStream(byteArray),
                _ => item.Source as Stream ?? throw new ArgumentException("Source must be a string file name, byte array, or Stream.", nameof(item.Source))
            };

            using Stream destination = destinationFileName.Create();
            DataStream.CopyPartTo(source, destination, item.Position, item.Length, BufferSize);
        };

        if (EnableThreading)
        {
            Parallel.ForEach(_items, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads }, action);
        }

        else
        {
            _items.ForEach(action);
        }

        _items.Clear();
    }

    private sealed class Item
    {
        public required object Source { get; set; }
        public required string DestinationFileName { get; set; }
        public long Position { get; set; }
        public long Length { get; set; }
    }
}
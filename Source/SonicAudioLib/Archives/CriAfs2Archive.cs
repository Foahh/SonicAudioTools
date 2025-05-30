using SonicAudioLib.IO;
using System;
using System.IO;
using System.Linq;

namespace SonicAudioLib.Archives;

public class CriAfs2Entry : EntryBase
{
    public uint Id { get; set; }
}

public class CriAfs2Archive : ArchiveBase<CriAfs2Entry>
{
    public ushort SubKey { get; set; }
    public ushort Align { get; set; } = 32;

    /// <summary>
    ///     Gets header of the written AFS2 archive.
    ///     Should be gotten after <see cref="Write(Stream)" /> is called.
    /// </summary>
    public byte[] Header { get; private set; }

    public override void Read(Stream source)
    {
        long ReadByLength(uint length)
        {
            switch (length)
            {
                case 2:
                    return DataStream.ReadUInt16(source);

                case 4:
                    return DataStream.ReadUInt32(source);

                case 8:
                    return DataStream.ReadInt64(source);

                default:
                    throw new ArgumentException($"Unimplemented field length ({length})", nameof(length));
            }
        }

        if (DataStream.ReadCString(source, 4) != "AFS2")
        {
            throw new InvalidDataException("'AFS2' signature could not be found.");
        }

        var information = DataStream.ReadUInt32(source);

        var type = information & 0xFF;
        if (type != 1 && type != 2)
        {
            throw new InvalidDataException($"Unknown AFS2 type ({type}). Please report this error with the file(s).");
        }

        var idFieldLength = information >> 16 & 0xFF;
        var positionFieldLength = information >> 8 & 0xFF;

        var entryCount = DataStream.ReadUInt32(source);
        Align = DataStream.ReadUInt16(source);
        SubKey = DataStream.ReadUInt16(source);

        CriAfs2Entry previousEntry = null;
        for (uint i = 0; i < entryCount; i++)
        {
            var afs2Entry = new CriAfs2Entry();

            long idPosition = 16 + i * idFieldLength;
            source.Seek(idPosition, SeekOrigin.Begin);
            afs2Entry.Id = (uint)ReadByLength(idFieldLength);

            long positionPosition = 16 + entryCount * idFieldLength + i * positionFieldLength;
            source.Seek(positionPosition, SeekOrigin.Begin);
            afs2Entry.Position = ReadByLength(positionFieldLength);

            if (previousEntry != null)
            {
                previousEntry.Length = afs2Entry.Position - previousEntry.Position;
            }

            afs2Entry.Position = Helpers.Align(afs2Entry.Position, Align);

            if (i == entryCount - 1)
            {
                afs2Entry.Length = ReadByLength(positionFieldLength) - afs2Entry.Position;
            }

            Entries.Add(afs2Entry);
            previousEntry = afs2Entry;
        }
    }

    public override void Write(Stream destination)
    {
        long GetHeaderLength(uint idFieldLen, uint positionFieldLen)
        {
            return 16 + idFieldLen * Entries.Count + positionFieldLen * Entries.Count + positionFieldLen;
        }

        long Calculate(out uint idFieldLen, out uint positionFieldLen)
        {
            // It's kind of impossible to have more than 65535 waveforms in one ACB, but just to make sure.
            idFieldLen = Entries.Count <= ushort.MaxValue ? 2u : 4u;

            long dataLength = 0;

            foreach (var entry in Entries)
            {
                dataLength = Helpers.Align(dataLength, Align);
                dataLength += entry.Length;
            }

            positionFieldLen = 2;
            long headerLen;

            // Check 2, 4 and 8
            if (Helpers.Align(headerLen = GetHeaderLength(idFieldLen, positionFieldLen), Align) + dataLength > ushort.MaxValue)
            {
                positionFieldLen = 4;

                if (Helpers.Align(headerLen = GetHeaderLength(idFieldLen, positionFieldLen), Align) + dataLength > uint.MaxValue)
                {
                    positionFieldLen = 8;
                }
            }

            return headerLen;
        }

        var mDestination = new MemoryStream();

        void WriteByLength(uint length, long value)
        {
            switch (length)
            {
                case 2:
                    DataStream.WriteUInt16(mDestination, (ushort)value);
                    break;

                case 4:
                    DataStream.WriteUInt32(mDestination, (uint)value);
                    break;

                case 8:
                    DataStream.WriteInt64(mDestination, value);
                    break;

                default:
                    throw new ArgumentException($"Unimplemented field length ({length})", nameof(length));
            }
        }

        var headerLength = Calculate(out var idFieldLength, out var positionFieldLength);

        DataStream.WriteCString(mDestination, "AFS2", 4);
        DataStream.WriteUInt32(mDestination, (SubKey != 0 ? 2 : 1u) | idFieldLength << 16 | positionFieldLength << 8);
        DataStream.WriteUInt32(mDestination, (uint)Entries.Count);
        DataStream.WriteUInt16(mDestination, Align);
        DataStream.WriteUInt16(mDestination, SubKey);

        var vldPool = new DataPool(Align, headerLength);
        vldPool.ProgressChanged += OnProgressChanged;

        var orderedEntries = Entries.OrderBy(entry => entry.Id).ToArray();
        foreach (var afs2Entry in orderedEntries)
        {
            WriteByLength(idFieldLength, afs2Entry.Id);
        }

        foreach (var afs2Entry in orderedEntries)
        {
            var entryPosition = vldPool.Length;
            vldPool.Put(afs2Entry.Stream);

            WriteByLength(positionFieldLength, entryPosition);
        }

        WriteByLength(positionFieldLength, vldPool.Length);
        
        Header = mDestination.ToArray();
        mDestination.Close();

        destination.Write(Header);
        vldPool.Write(destination);
        vldPool.Clear();
    }

    public CriAfs2Entry GetById(uint id)
    {
        return Entries.FirstOrDefault(e => e.Id == id);
    }
}
using System.IO;

namespace SonicAudioLib.IO;

public sealed class SubStream : Stream
{
    private readonly long _basePosition;
    private readonly Stream _baseStream;
    private long _baseLength;

    public SubStream(Stream baseStream, long basePosition) : this(baseStream, basePosition, baseStream.Length - basePosition)
    {
    }

    public SubStream(Stream baseStream, long basePosition, long baseLength)
    {
        this._baseStream = baseStream;
        this._basePosition = basePosition;
        this._baseLength = baseLength;

        baseStream.Seek(this._basePosition, SeekOrigin.Begin);
    }

    public override bool CanRead => _baseStream.CanRead;

    public override bool CanSeek => _baseStream.CanSeek;

    public override bool CanWrite => _baseStream.CanWrite;

    public override long Length => _baseLength;

    public override long Position
    {
        get => _baseStream.Position - _basePosition;

        set => _baseStream.Position = _basePosition + value;
    }

    public override void Flush()
    {
        _baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_baseStream.Position >= _basePosition + _baseLength)
        {
            count = 0;
        }

        else if (_baseStream.Position + count > _basePosition + _baseLength)
        {
            count = (int)(_basePosition + _baseLength - _baseStream.Position);
        }

        return _baseStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Begin)
        {
            offset += _basePosition;
        }

        else if (origin == SeekOrigin.End)
        {
            offset = _basePosition + _baseLength - offset;
            origin = SeekOrigin.Begin;
        }

        return _baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _baseLength = value;

        if (_basePosition + _baseLength > _baseStream.Length)
        {
            _baseStream.SetLength(_basePosition + _baseLength);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_baseStream.Position >= _basePosition + _baseLength)
        {
            count = 0;
        }

        else if (_baseStream.Position + count > _basePosition + _baseLength)
        {
            count = (int)(_basePosition + _baseLength - _baseStream.Position);
        }

        _baseStream.Write(buffer, 0, count);
    }

    public byte[] ToArray()
    {
        var previousPosition = _baseStream.Position;

        _baseStream.Seek(_basePosition, SeekOrigin.Begin);

        using var memoryStream = new MemoryStream();
        CopyTo(memoryStream);

        _baseStream.Seek(previousPosition, SeekOrigin.Begin);
        return memoryStream.ToArray();
    }
}
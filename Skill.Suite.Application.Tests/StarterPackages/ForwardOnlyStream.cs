namespace Skill.Suite.Application.Tests.StarterPackages;

/// <summary>
/// Hands its content out a few bytes at a time and can neither seek nor say how long it is — the shape of a
/// browser upload arriving over the circuit.
/// </summary>
/// <param name="afterRead">
/// Called with the number of bytes handed out so far after every read, so a test can act while the upload is
/// still arriving: cancel it, or put something where it is going.
/// </param>
internal sealed class ForwardOnlyStream(byte[] content, int chunkSize, Action<int>? afterRead = null) : Stream
{
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = Math.Min(Math.Min(buffer.Length, chunkSize), content.Length - _position);
        content.AsSpan(_position, count).CopyTo(buffer);
        _position += count;

        afterRead?.Invoke(_position);
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<int>(cancellationToken)
            : ValueTask.FromResult(Read(buffer.Span));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

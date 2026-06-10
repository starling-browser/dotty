namespace Dotty.Terminal.Pty;

/// <summary>
/// Windows ConPTY stub.
/// </summary>
public sealed class WindowsPty : IPty
{
    public static WindowsPty Spawn(PtyConfig config)
    {
        throw new PlatformNotSupportedException("Windows ConPTY support is not yet implemented.");
    }

    public int Read(Span<byte> buffer) => throw new PlatformNotSupportedException();
    public void Write(ReadOnlySpan<byte> data) => throw new PlatformNotSupportedException();
    public void Resize(GridSize size) => throw new PlatformNotSupportedException();
    public int? TryWaitExit() => throw new PlatformNotSupportedException();
    public void Dispose() { }
}

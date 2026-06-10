namespace Dotty.Terminal.Pty;

public interface IPty : IDisposable
{
    int Read(Span<byte> buffer);
    void Write(ReadOnlySpan<byte> data);
    void Resize(GridSize size);
    int? TryWaitExit();
}

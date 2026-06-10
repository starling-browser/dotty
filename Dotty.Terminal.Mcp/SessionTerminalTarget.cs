using Dotty.Terminal.Hosting;

namespace Dotty.Terminal.Mcp;

public sealed class SessionTerminalTarget : ITerminalTarget
{
    private readonly TerminalSession _session;

    public SessionTerminalTarget(TerminalSession session)
    {
        _session = session;
    }

    public string GetVisibleText() => _session.GetVisibleText();

    public ValueTask WriteAsync(byte[] data, CancellationToken cancellationToken = default) =>
        _session.WriteAsync(data, cancellationToken);
}

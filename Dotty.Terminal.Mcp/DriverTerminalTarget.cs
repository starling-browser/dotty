using Dotty.Terminal.Pty;

namespace Dotty.Terminal.Mcp;

/// <summary>
/// Default <see cref="ITerminalTarget"/> that binds the MCP terminal tools directly
/// to a <see cref="TerminalDriver"/>. Use this for the common single-terminal embedding
/// where no thread marshaling is required.
/// </summary>
public sealed class DriverTerminalTarget : ITerminalTarget
{
    private readonly TerminalDriver _driver;

    public DriverTerminalTarget(TerminalDriver driver)
    {
        _driver = driver;
    }

    public string GetVisibleText() => TerminalText.GetVisibleText(_driver.Terminal);

    public ValueTask WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        _driver.WriteToPty(data);
        return ValueTask.CompletedTask;
    }
}

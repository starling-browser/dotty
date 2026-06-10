namespace Dotty.Terminal.Mcp;

/// <summary>
/// Abstraction over a single embedded terminal that the MCP terminal tools act on.
/// A host implements this to expose whichever terminal it wants the tools to reach
/// (for example the active tab) and to marshal calls onto the right thread.
/// </summary>
public interface ITerminalTarget
{
    /// <summary>
    /// Returns the visible text currently on the terminal screen.
    /// </summary>
    string GetVisibleText();

    /// <summary>
    /// Sends raw bytes to the terminal's PTY, as if typed by the user.
    /// </summary>
    ValueTask WriteAsync(byte[] data, CancellationToken cancellationToken = default);
}

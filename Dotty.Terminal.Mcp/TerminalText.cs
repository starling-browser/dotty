using TerminalEngine = Dotty.Terminal.Terminal;

namespace Dotty.Terminal.Mcp;

/// <summary>
/// Extracts plain text from the terminal grid. UI-framework independent: it reads
/// only the engine's public cell data, so any host can produce the same screen text.
/// </summary>
public static class TerminalText
{
    public static string GetVisibleText(TerminalEngine terminal) =>
        Dotty.Terminal.Rendering.TerminalText.GetVisibleText(terminal);
}

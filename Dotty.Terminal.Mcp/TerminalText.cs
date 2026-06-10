using System.Text;
using TerminalEngine = Dotty.Terminal.Terminal;

namespace Dotty.Terminal.Mcp;

/// <summary>
/// Extracts plain text from the terminal grid. UI-framework independent: it reads
/// only the engine's public cell data, so any host can produce the same screen text.
/// </summary>
public static class TerminalText
{
    /// <summary>
    /// Builds the visible screen text from the terminal's current grid, trimming
    /// trailing whitespace per line and dropping fully empty trailing lines.
    /// </summary>
    public static string GetVisibleText(TerminalEngine terminal)
    {
        var size = terminal.GridSize;
        var sb = new StringBuilder();

        for (ushort row = 0; row < size.Rows; row++)
        {
            var cells = terminal.RowCells(row);
            var line = new StringBuilder();
            for (int col = 0; col < cells.Length; col++)
                line.Append(cells[col].Codepoint);

            sb.AppendLine(line.ToString().TrimEnd());
        }

        var result = sb.ToString().TrimEnd('\r', '\n');
        return result.Length > 0 ? result + "\n" : "";
    }
}

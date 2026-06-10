using System.Text;

namespace Dotty.Terminal.Rendering;

public static class TerminalText
{
    public static string GetVisibleText(Terminal terminal)
    {
        var size = terminal.GridSize;
        var sb = new StringBuilder();

        for (ushort row = 0; row < size.Rows; row++)
        {
            var cells = terminal.ViewportRowCells(row);
            var line = new StringBuilder();
            for (int col = 0; col < cells.Length; col++)
                line.Append(cells[col].Codepoint);

            sb.AppendLine(line.ToString().TrimEnd());
        }

        var result = sb.ToString().TrimEnd('\r', '\n');
        return result.Length > 0 ? result + "\n" : "";
    }
}

using Dotty.Terminal.Rendering;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TerminalScreenSnapshotTests
{
    [Fact]
    public void SnapshotCapturesVisibleCells()
    {
        var terminal = new Terminal(new GridSize(10, 2));
        terminal.ProcessPtyOutput("Hi"u8);

        var snapshot = TerminalScreenSnapshot.FromTerminal(terminal);

        Assert.Equal(new GridSize(10, 2), snapshot.Size);
        Assert.Equal('H', snapshot.CellAt(0, 0).Codepoint);
        Assert.Equal('i', snapshot.CellAt(1, 0).Codepoint);
    }

    [Fact]
    public void SnapshotCapturesSelection()
    {
        var terminal = new Terminal(new GridSize(10, 2));
        terminal.ProcessPtyOutput("Hi"u8);
        terminal.StartSelection(new GridPosition(0, 0), SelectionMode.Normal);
        terminal.UpdateSelection(new GridPosition(1, 0));

        var snapshot = TerminalScreenSnapshot.FromTerminal(terminal);

        Assert.True(snapshot.CellAt(0, 0).IsSelected);
        Assert.True(snapshot.CellAt(1, 0).IsSelected);
        Assert.False(snapshot.CellAt(2, 0).IsSelected);
    }

    [Fact]
    public void TerminalTextReadsVisibleRows()
    {
        var terminal = new Terminal(new GridSize(10, 2));
        terminal.ProcessPtyOutput("ready>"u8);

        var text = TerminalText.GetVisibleText(terminal);

        Assert.Contains("ready>", text);
    }
}

using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class GridTests
{
    [Fact]
    public void NewGridHasCorrectSize()
    {
        var grid = new Grid(new GridSize(80, 24));
        Assert.Equal(new GridSize(80, 24), grid.Size);
    }

    [Fact]
    public void CellAccessWorks()
    {
        var grid = new Grid(new GridSize(10, 5));
        grid.CellAt(3, 2).Codepoint = 'X';
        Assert.Equal('X', grid.CellAt(3, 2).Codepoint);
    }

    [Fact]
    public void ClearRow()
    {
        var grid = new Grid(new GridSize(10, 5));
        grid.CellAt(0, 0).Codepoint = 'A';
        grid.CellAt(5, 0).Codepoint = 'B';
        grid.ClearRow(0);
        Assert.Equal(' ', grid.CellAt(0, 0).Codepoint);
        Assert.Equal(' ', grid.CellAt(5, 0).Codepoint);
    }

    [Fact]
    public void ScrollUpReturnsScrolledRows()
    {
        var grid = new Grid(new GridSize(5, 3));
        grid.CellAt(0, 0).Codepoint = 'A';
        grid.CellAt(0, 1).Codepoint = 'B';
        grid.CellAt(0, 2).Codepoint = 'C';

        var scrolled = grid.ScrollUp(1);
        Assert.Single(scrolled);
        Assert.Equal('A', scrolled[0].Cells[0].Codepoint);
        Assert.Equal('B', grid.CellAt(0, 0).Codepoint);
        Assert.Equal('C', grid.CellAt(0, 1).Codepoint);
        Assert.Equal(' ', grid.CellAt(0, 2).Codepoint);
    }

    [Fact]
    public void ScrollDown()
    {
        var grid = new Grid(new GridSize(5, 3));
        grid.CellAt(0, 0).Codepoint = 'A';
        grid.CellAt(0, 1).Codepoint = 'B';

        grid.ScrollDown(1);
        Assert.Equal(' ', grid.CellAt(0, 0).Codepoint);
        Assert.Equal('A', grid.CellAt(0, 1).Codepoint);
        Assert.Equal('B', grid.CellAt(0, 2).Codepoint);
    }

    [Fact]
    public void InsertCells()
    {
        var grid = new Grid(new GridSize(5, 1));
        grid.CellAt(0, 0).Codepoint = 'A';
        grid.CellAt(1, 0).Codepoint = 'B';
        grid.CellAt(2, 0).Codepoint = 'C';

        grid.InsertCells(1, 0, 2);
        Assert.Equal('A', grid.CellAt(0, 0).Codepoint);
        Assert.Equal(' ', grid.CellAt(1, 0).Codepoint);
        Assert.Equal(' ', grid.CellAt(2, 0).Codepoint);
        Assert.Equal('B', grid.CellAt(3, 0).Codepoint);
    }

    [Fact]
    public void DeleteCells()
    {
        var grid = new Grid(new GridSize(5, 1));
        grid.CellAt(0, 0).Codepoint = 'A';
        grid.CellAt(1, 0).Codepoint = 'B';
        grid.CellAt(2, 0).Codepoint = 'C';
        grid.CellAt(3, 0).Codepoint = 'D';

        grid.DeleteCells(1, 0, 2);
        Assert.Equal('A', grid.CellAt(0, 0).Codepoint);
        Assert.Equal('D', grid.CellAt(1, 0).Codepoint);
        Assert.Equal(' ', grid.CellAt(2, 0).Codepoint);
    }

    [Fact]
    public void ScrollRegion()
    {
        var grid = new Grid(new GridSize(5, 5));
        grid.SetScrollRegion(1, 4);
        Assert.Equal((ushort)1, grid.ScrollTop);
        Assert.Equal((ushort)4, grid.ScrollBottom);

        grid.CellAt(0, 1).Codepoint = 'A';
        grid.CellAt(0, 2).Codepoint = 'B';
        grid.CellAt(0, 3).Codepoint = 'C';

        var scrolled = grid.ScrollUp(1);
        Assert.Single(scrolled);
        Assert.Equal('A', scrolled[0].Cells[0].Codepoint);
        Assert.Equal('B', grid.CellAt(0, 1).Codepoint);
        Assert.Equal('C', grid.CellAt(0, 2).Codepoint);
        Assert.Equal(' ', grid.CellAt(0, 3).Codepoint);
    }
}

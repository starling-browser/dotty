using Dotty.Rendering;
using Xunit;

namespace Dotty.Tests;

public class CellMetricsTests
{
    // ── Cell dimensions ──

    [Fact]
    public void CellDimensionsMatchFontSize()
    {
        var metrics = new CellMetrics(14.0);

        Assert.Equal(14.0, metrics.FontSize);
        Assert.Equal(20.0, metrics.PadX);
        Assert.Equal(16.0, metrics.PadY);
    }

    // ── Bug-confirming: cell dimensions must be whole pixels ──
    // Sub-pixel cell dimensions cause anti-aliased seams between rows/columns,
    // producing visible horizontal line artifacts during rendering.

    [Theory]
    [InlineData(8.0)]
    [InlineData(12.0)]
    [InlineData(14.0)]
    [InlineData(16.0)]  // default font size
    [InlineData(20.0)]
    [InlineData(24.0)]
    [InlineData(32.0)]
    [InlineData(48.0)]
    public void CellDimensionsAreWholePixels(double fontSize)
    {
        var metrics = new CellMetrics(fontSize);

        Assert.True(metrics.CellWidth == Math.Floor(metrics.CellWidth),
            $"CellWidth {metrics.CellWidth} is not a whole pixel at fontSize {fontSize}");
        Assert.True(metrics.CellHeight == Math.Floor(metrics.CellHeight),
            $"CellHeight {metrics.CellHeight} is not a whole pixel at fontSize {fontSize}");
    }

    [Theory]
    [InlineData(14.0)]
    [InlineData(16.0)]  // default
    [InlineData(20.0)]
    public void AllRowPositionsAreIntegerPixels(double fontSize)
    {
        var metrics = new CellMetrics(fontSize);
        ushort rows = metrics.RowsForHeight(1200.0);

        for (ushort row = 0; row < rows; row++)
        {
            double y = metrics.PadY + row * metrics.CellHeight;
            Assert.True(y == Math.Floor(y),
                $"Row {row} position y={y} is not an integer pixel (fontSize={fontSize})");
        }
    }

    [Theory]
    [InlineData(14.0)]
    [InlineData(16.0)]  // default
    [InlineData(20.0)]
    public void AllColumnPositionsAreIntegerPixels(double fontSize)
    {
        var metrics = new CellMetrics(fontSize);
        ushort cols = metrics.ColumnsForWidth(1600.0);

        for (ushort col = 0; col < cols; col++)
        {
            double x = metrics.PadX + col * metrics.CellWidth;
            Assert.True(x == Math.Floor(x),
                $"Column {col} position x={x} is not an integer pixel (fontSize={fontSize})");
        }
    }

    // ── Grid dimensions from window bounds ──
    // ColumnsForWidth subtracts 2*PadX (40px), RowsForHeight subtracts 2*PadY (32px)

    [Fact]
    public void ColumnsForWidth()
    {
        var metrics = new CellMetrics(10.0);
        // (100 - 40) / CellWidth = 60 / CellWidth
        ushort cols = metrics.ColumnsForWidth(100.0);
        Assert.Equal((ushort)(60.0 / metrics.CellWidth), cols);
    }

    [Fact]
    public void RowsForHeight()
    {
        var metrics = new CellMetrics(10.0);
        // (100 - 32) / CellHeight = 68 / CellHeight
        ushort rows = metrics.RowsForHeight(100.0);
        Assert.Equal((ushort)(68.0 / metrics.CellHeight), rows);
    }

    [Fact]
    public void PartialCellTruncated()
    {
        var metrics = new CellMetrics(10.0);
        // Width smaller than 2*PadX → clamped to minimum 1
        ushort cols = metrics.ColumnsForWidth(37.0);
        Assert.Equal((ushort)1, cols);
    }

    [Fact]
    public void MinimumGridSizeIsOneByOne()
    {
        var metrics = new CellMetrics(14.0);
        // Very small dimensions: less than padding
        Assert.Equal((ushort)1, metrics.ColumnsForWidth(1.0));
        Assert.Equal((ushort)1, metrics.RowsForHeight(1.0));
    }

    [Fact]
    public void ZeroWidthReturnsMinimum()
    {
        var metrics = new CellMetrics(14.0);
        Assert.Equal((ushort)1, metrics.ColumnsForWidth(0.0));
        Assert.Equal((ushort)1, metrics.RowsForHeight(0.0));
    }

    // ── Text pixel position calculations ──
    // The renderer draws each cell at: x = padX + col * cellWidth, y = padY + row * cellHeight

    [Fact]
    public void TextAtOriginDrawnAtPadding()
    {
        var metrics = new CellMetrics(14.0);
        double x = metrics.PadX + 0 * metrics.CellWidth;
        double y = metrics.PadY + 0 * metrics.CellHeight;

        Assert.Equal(20.0, x);
        Assert.Equal(16.0, y);
    }

    // ── Window resize scenarios ──

    [Fact]
    public void WindowResizeChangesGridDimensions()
    {
        var metrics = new CellMetrics(14.0);

        ushort cols1 = metrics.ColumnsForWidth(200.0);
        ushort rows1 = metrics.RowsForHeight(200.0);
        ushort cols2 = metrics.ColumnsForWidth(400.0);
        ushort rows2 = metrics.RowsForHeight(400.0);

        Assert.True(cols2 > cols1);
        Assert.True(rows2 > rows1);
    }

    [Fact]
    public void WindowMoveDoesNotChangeGridDimensions()
    {
        var metrics = new CellMetrics(14.0);

        ushort cols = metrics.ColumnsForWidth(500.0);
        ushort rows = metrics.RowsForHeight(300.0);
        ushort colsAfter = metrics.ColumnsForWidth(500.0);
        ushort rowsAfter = metrics.RowsForHeight(300.0);

        Assert.Equal(cols, colsAfter);
        Assert.Equal(rows, rowsAfter);
    }

    // ── Text position shifts under resize ──

    [Fact]
    public void TextPixelPositionScalesWithFontSize()
    {
        var small = new CellMetrics(10.0);
        var large = new CellMetrics(20.0);

        // Cell dimensions should scale 2x with 2x font size
        Assert.Equal(small.CellWidth * 2, large.CellWidth, precision: 5);
        Assert.Equal(small.CellHeight * 2, large.CellHeight, precision: 5);
    }

    [Fact]
    public void LastVisibleCellIsWithinBounds()
    {
        var metrics = new CellMetrics(14.0);
        ushort cols = metrics.ColumnsForWidth(800.0);
        ushort rows = metrics.RowsForHeight(600.0);

        // Last cell's bottom-right corner should be within window bounds
        double lastRight = metrics.PadX + cols * metrics.CellWidth;
        double lastBottom = metrics.PadY + rows * metrics.CellHeight;

        Assert.True(lastRight <= 800.0, $"Last column extends to {lastRight} which exceeds width 800");
        Assert.True(lastBottom <= 600.0, $"Last row extends to {lastBottom} which exceeds height 600");
    }

    // ── Bug-confirming: resize/font-change mismatch tests ──
    // During resize debounce, Render() computes new CellMetrics but uses the terminal's
    // old grid dimensions. This causes rendered content to overflow window bounds.

    [Fact]
    public void FontIncreaseRenderedContentOverflowsBounds()
    {
        // Compute grid dimensions at font 16, 800×600
        var oldMetrics = new CellMetrics(16.0);
        ushort cols = oldMetrics.ColumnsForWidth(800.0);
        ushort rows = oldMetrics.RowsForHeight(600.0);

        // Font changes to 20 — new metrics, but grid dimensions haven't updated yet
        var newMetrics = new CellMetrics(20.0);

        // Rendering old grid with new metrics: content must fit within 800×600
        double renderedWidth = newMetrics.PadX + cols * newMetrics.CellWidth;
        double renderedHeight = newMetrics.PadY + rows * newMetrics.CellHeight;

        Assert.True(renderedWidth <= 800.0,
            $"Rendered width {renderedWidth} exceeds window width 800 (old grid {cols}×{rows} with new font metrics)");
        Assert.True(renderedHeight <= 600.0,
            $"Rendered height {renderedHeight} exceeds window height 600 (old grid {cols}×{rows} with new font metrics)");
    }

    [Fact]
    public void WindowShrinkRenderedContentOverflowsBounds()
    {
        // Compute grid dimensions at font 16, 800×600
        var metrics = new CellMetrics(16.0);
        ushort cols = metrics.ColumnsForWidth(800.0);
        ushort rows = metrics.RowsForHeight(600.0);

        // Window shrinks to 600×400, but grid dimensions haven't updated yet
        double renderedWidth = metrics.PadX + cols * metrics.CellWidth;
        double renderedHeight = metrics.PadY + rows * metrics.CellHeight;

        Assert.True(renderedWidth <= 600.0,
            $"Rendered width {renderedWidth} exceeds new window width 600 (stale grid {cols}×{rows})");
        Assert.True(renderedHeight <= 400.0,
            $"Rendered height {renderedHeight} exceeds new window height 400 (stale grid {cols}×{rows})");
    }

    [Fact]
    public void FontDecreaseRenderedContentLeavesUnusedSpace()
    {
        // Compute grid dimensions at font 20, 800×600
        var oldMetrics = new CellMetrics(20.0);
        ushort cols = oldMetrics.ColumnsForWidth(800.0);
        ushort rows = oldMetrics.RowsForHeight(600.0);

        // Font changes to 16 — smaller cells, but grid dimensions are stale
        var newMetrics = new CellMetrics(16.0);

        double renderedWidth = newMetrics.PadX + cols * newMetrics.CellWidth;
        double renderedHeight = newMetrics.PadY + rows * newMetrics.CellHeight;

        // Content fits (no overflow) but wastes significant space
        Assert.True(renderedWidth <= 800.0,
            $"Rendered width {renderedWidth} exceeds 800 — expected to fit");
        Assert.True(renderedHeight <= 600.0,
            $"Rendered height {renderedHeight} exceeds 600 — expected to fit");

        // Document the wasted space: at least 10% unused in each dimension
        double unusedWidth = 800.0 - renderedWidth;
        double unusedHeight = 600.0 - renderedHeight;
        Assert.True(unusedWidth / 800.0 > 0.10,
            $"Only {unusedWidth / 800.0 * 100:F1}% unused width — mismatch wastes less space than expected");
        Assert.True(unusedHeight / 600.0 > 0.10,
            $"Only {unusedHeight / 600.0 * 100:F1}% unused height — mismatch wastes less space than expected");
    }
}

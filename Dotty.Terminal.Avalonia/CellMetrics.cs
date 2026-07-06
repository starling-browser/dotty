using GridSize = Dotty.Terminal.GridSize;

namespace Dotty.Rendering;

public readonly struct CellMetrics
{
    public double CellWidth { get; }
    public double CellHeight { get; }
    public double FontSize { get; }

    public double PadX { get; }
    public double PadY { get; }

    private const double WidthRatio = 0.6;
    private const double HeightRatio = 1.2;
    private const double DefaultPadX = 20;
    private const double DefaultPadY = 16;

    public CellMetrics(double fontSize)
    {
        FontSize = fontSize;
        CellWidth = Math.Ceiling(fontSize * WidthRatio);
        CellHeight = Math.Ceiling(fontSize * HeightRatio);
        PadX = DefaultPadX;
        PadY = DefaultPadY;
    }

    public ushort ColumnsForWidth(double width) => (ushort)Math.Max(1, (width - PadX * 2) / CellWidth);
    public ushort RowsForHeight(double height) => (ushort)Math.Max(1, (height - PadY * 2) / CellHeight);

    public double RenderedContentWidth(ushort columns) => PadX + columns * CellWidth;
    public double RenderedContentHeight(ushort rows) => PadY + rows * CellHeight;

    public bool FitsWithin(GridSize size, double width, double height) =>
        RenderedContentWidth(size.Cols) <= width
        && RenderedContentHeight(size.Rows) <= height;
}

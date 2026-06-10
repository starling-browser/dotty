using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Dotty.Theme;
using Dotty.Terminal.Rendering;
using GridPosition = Dotty.Terminal.GridPosition;
using CellAttributes = Dotty.Terminal.CellAttributes;
using CursorShape = Dotty.Terminal.CursorShape;
using ColorKind = Dotty.Terminal.ColorKind;

namespace Dotty.Rendering;

public static class TerminalRenderer
{
    // Per-frame brush cache to avoid allocating thousands of identical brushes
    private static readonly Dictionary<uint, SolidColorBrush> _brushCache = new();

    private static SolidColorBrush GetBrush(Color color)
    {
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        if (!_brushCache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(color);
            _brushCache[key] = brush;
        }
        return brush;
    }

    public static void Draw(
        DrawingContext context,
        TerminalScreenSnapshot snapshot,
        TerminalTheme theme,
        CellMetrics metrics,
        Size bounds,
        bool cursorBlinkVisible = true,
        bool bellFlash = false)
    {
        var palette = theme.Palette;
        double cellW = metrics.CellWidth;
        double cellH = metrics.CellHeight;

        // Draw background
        var bgColor = theme.ToAvaloniaColor(palette.Background);
        context.FillRectangle(GetBrush(bgColor), new Rect(0, 0, bounds.Width, bounds.Height));

        var cursor = snapshot.Cursor;
        ushort cols = snapshot.Size.Cols;
        ushort rows = snapshot.Size.Rows;

        // Pre-compute colors
        var cursorColor = theme.ToAvaloniaColor(palette.Cursor);
        var baseColor = theme.ToAvaloniaColor(palette.Base);
        var lavenderColor = theme.ToAvaloniaColor(palette.Lavender);
        var selectionColor = Color.FromArgb(46, lavenderColor.R, lavenderColor.G, lavenderColor.B); // ~18% opacity
        var glowColor = Color.FromArgb(0x28, cursorColor.R, cursorColor.G, cursorColor.B);
        var glowBrush = GetBrush(glowColor);
        const double glowPad = 2;

        var fontFamily = FontFamily.Parse("fonts:DottyTerminal#Overpass Mono, Cascadia Mono, Menlo, monospace");
        var typeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Light);
        var boldTypeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Regular);
        var italicTypeface = new Typeface(fontFamily, FontStyle.Italic, FontWeight.Light);
        var boldItalicTypeface = new Typeface(fontFamily, FontStyle.Italic, FontWeight.Regular);

        var selBrush = GetBrush(selectionColor);
        var cursorBrush = GetBrush(cursorColor);

        double padX = metrics.PadX;
        double padY = metrics.PadY;

        bool isScrolledBack = snapshot.IsScrolledBack;

        for (ushort row = 0; row < rows; row++)
        {
            double cellY = padY + row * cellH;

            for (ushort col = 0; col < cols; col++)
            {
                var cell = snapshot.CellAt(col, row);
                double cellX = padX + col * cellW;

                bool isCursor = !isScrolledBack
                    && cursor.Visible
                    && cursor.Position.Col == col
                    && cursor.Position.Row == row
                    && (cursorBlinkVisible || !cursor.Blinking);

                bool isSelected = cell.IsSelected;

                bool inverse = cell.Attributes.HasFlag(CellAttributes.Inverse);

                // Resolve background color
                var bg = inverse
                    ? cell.Foreground.ToRgb(palette)
                    : cell.Background.ToBgRgb(palette);

                // Bold text brightness: ANSI 0-7 -> 8-15
                var fg = inverse
                    ? cell.Background.ToBgRgb(palette)
                    : cell.Attributes.HasFlag(CellAttributes.Bold) && cell.Foreground.Kind == ColorKind.Ansi && cell.Foreground.R < 8
                        ? palette.Colors[cell.Foreground.R + 8]
                        : cell.Foreground.ToRgb(palette);

                // Draw cell background
                if (bg != palette.Background)
                {
                    context.FillRectangle(GetBrush(Color.FromRgb(bg.R, bg.G, bg.B)),
                        new Rect(cellX, cellY, cellW, cellH));
                }

                // Draw selection overlay
                if (isSelected)
                {
                    context.FillRectangle(selBrush, new Rect(cellX, cellY, cellW, cellH));
                }

                // Draw cursor glow aura, then cursor
                if (isCursor)
                {
                    // Glow layer (drawn first, behind cursor)
                    switch (cursor.Shape)
                    {
                        case CursorShape.Block:
                            context.FillRectangle(glowBrush, new Rect(cellX - glowPad, cellY - glowPad, cellW + glowPad * 2, cellH + glowPad * 2));
                            break;
                        case CursorShape.Underline:
                            context.FillRectangle(glowBrush, new Rect(cellX - glowPad, cellY + cellH - 4, cellW + glowPad * 2, 6));
                            break;
                        case CursorShape.Bar:
                            context.FillRectangle(glowBrush, new Rect(cellX - glowPad, cellY - glowPad, 4 + glowPad, cellH + glowPad * 2));
                            break;
                    }

                    // Actual cursor
                    switch (cursor.Shape)
                    {
                        case CursorShape.Block:
                            context.FillRectangle(cursorBrush, new Rect(cellX, cellY, cellW, cellH));
                            break;
                        case CursorShape.Underline:
                            context.FillRectangle(cursorBrush, new Rect(cellX, cellY + cellH - 2, cellW, 2));
                            break;
                        case CursorShape.Bar:
                            context.FillRectangle(cursorBrush, new Rect(cellX, cellY, 2, cellH));
                            break;
                    }
                }

                // Hidden text: skip rendering entirely
                bool hidden = cell.Attributes.HasFlag(CellAttributes.Hidden);

                // Draw text
                if (cell.Codepoint != ' ' && !hidden)
                {
                    var textColor = isCursor && cursor.Shape == CursorShape.Block
                        ? baseColor
                        : Color.FromRgb(fg.R, fg.G, fg.B);

                    // Dim: reduce alpha to ~50%
                    if (cell.Attributes.HasFlag(CellAttributes.Dim))
                        textColor = Color.FromArgb(0x80, textColor.R, textColor.G, textColor.B);

                    bool isBold = cell.Attributes.HasFlag(CellAttributes.Bold);
                    bool isItalic = cell.Attributes.HasFlag(CellAttributes.Italic);
                    var tf = (isBold, isItalic) switch
                    {
                        (true, true) => boldItalicTypeface,
                        (true, false) => boldTypeface,
                        (false, true) => italicTypeface,
                        _ => typeface,
                    };

                    var formattedText = new FormattedText(
                        cell.Codepoint.ToString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        tf,
                        metrics.FontSize,
                        GetBrush(textColor));

                    context.DrawText(formattedText, new Point(cellX, cellY));
                }

                // Underline
                if (cell.Attributes.HasFlag(CellAttributes.Underline) && !hidden)
                {
                    var ulColor = isCursor && cursor.Shape == CursorShape.Block
                        ? baseColor
                        : Color.FromRgb(fg.R, fg.G, fg.B);
                    if (cell.Attributes.HasFlag(CellAttributes.Dim))
                        ulColor = Color.FromArgb(0x80, ulColor.R, ulColor.G, ulColor.B);
                    double ulY = cellY + cellH - 1.5;
                    var pen = new Pen(GetBrush(ulColor), 1);
                    context.DrawLine(pen, new Point(cellX, ulY), new Point(cellX + cellW, ulY));
                }

                // Strikethrough
                if (cell.Attributes.HasFlag(CellAttributes.Strikethrough) && !hidden)
                {
                    var stColor = isCursor && cursor.Shape == CursorShape.Block
                        ? baseColor
                        : Color.FromRgb(fg.R, fg.G, fg.B);
                    if (cell.Attributes.HasFlag(CellAttributes.Dim))
                        stColor = Color.FromArgb(0x80, stColor.R, stColor.G, stColor.B);
                    double stY = cellY + cellH * 0.5;
                    var pen = new Pen(GetBrush(stColor), 1);
                    context.DrawLine(pen, new Point(cellX, stY), new Point(cellX + cellW, stY));
                }
            }
        }

        // Draw visual bell flash overlay
        if (bellFlash)
        {
            var flashBrush = GetBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            context.FillRectangle(flashBrush, new Rect(0, 0, bounds.Width, bounds.Height));
        }

        // Draw exit status message at bottom
        if (snapshot.ExitMessage != null)
        {
            var exitTypeface = new Typeface(FontFamily.Parse("fonts:DottyTerminal#Overpass Mono, Cascadia Mono, Menlo, monospace"), FontStyle.Normal, FontWeight.Light);
            var exitColor = Color.FromArgb(0xCC, 0xAA, 0xAA, 0xAA);
            var exitText = new FormattedText(
                snapshot.ExitMessage,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                exitTypeface,
                metrics.FontSize * 0.85,
                GetBrush(exitColor));

            double exitY = padY + rows * cellH + 4;
            context.DrawText(exitText, new Point(padX, exitY));
        }
    }
}

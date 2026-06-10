using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Dotty.Controls;

/// <summary>
/// Full-window overlay rendering subtle CRT scan-line effect.
/// Renders horizontal lines every 2px at 3% black opacity.
/// </summary>
public class ScanLineOverlay : Control
{
    private static readonly IBrush LineBrush = new SolidColorBrush(Color.FromArgb(0x08, 0x00, 0x00, 0x00)); // ~3% black

    public ScanLineOverlay()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        for (double y = 0; y < bounds.Height; y += 2)
        {
            context.FillRectangle(LineBrush, new Rect(0, y, bounds.Width, 1));
        }
    }
}

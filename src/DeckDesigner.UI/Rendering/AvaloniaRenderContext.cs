using Avalonia;
using Avalonia.Media;
using DeckDesigner.Rendering;

namespace DeckDesigner.UI.Rendering;

/// <summary>
/// Bridges the toolkit-agnostic <see cref="IRenderContext"/> to an Avalonia
/// <see cref="DrawingContext"/> so the existing passive renderers can draw onto the canvas.
/// </summary>
public sealed class AvaloniaRenderContext(DrawingContext context) : IRenderContext
{
    private readonly DrawingContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly Dictionary<string, IPen> _penCache = new();
    private readonly Dictionary<string, IBrush> _brushCache = new();

    public void DrawLine(double x1, double y1, double x2, double y2, string colorHex, double thickness)
    {
        var pen = GetPen(colorHex, thickness);
        _context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
    }

    public void DrawPolygon(IReadOnlyList<(double X, double Y)> points, string strokeHex, double thickness, string? fillHex = null)
    {
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), fillHex is not null);
            for (var i = 1; i < points.Count; i++)
            {
                ctx.LineTo(new Point(points[i].X, points[i].Y));
            }

            ctx.EndFigure(true);
        }

        var fill = fillHex is null ? null : GetBrush(fillHex);
        _context.DrawGeometry(fill, GetPen(strokeHex, thickness), geometry);
    }

    public void DrawCircle(double x, double y, double radius, string colorHex, double thickness)
    {
        _context.DrawEllipse(null, GetPen(colorHex, thickness), new Point(x, y), radius, radius);
    }

    private IPen GetPen(string colorHex, double thickness)
    {
        var key = $"{colorHex}:{thickness}";
        if (!_penCache.TryGetValue(key, out var pen))
        {
            pen = new Pen(GetBrush(colorHex), thickness);
            _penCache[key] = pen;
        }

        return pen;
    }

    private IBrush GetBrush(string colorHex)
    {
        if (!_brushCache.TryGetValue(colorHex, out var brush))
        {
            brush = new SolidColorBrush(Color.Parse(colorHex));
            _brushCache[colorHex] = brush;
        }

        return brush;
    }
}

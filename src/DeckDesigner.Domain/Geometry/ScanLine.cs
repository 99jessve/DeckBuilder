namespace DeckDesigner.Domain.Geometry;

/// <summary>
/// Computes intersection spans of axis-aligned scan lines with a polygon.
/// </summary>
public static class ScanLine
{
    public readonly record struct Span(double Start, double End)
    {
        public double Length => End - Start;
    }

    /// <summary>
    /// Intersects a horizontal line (constant Y) with the polygon and returns the interior spans in X.
    /// </summary>
    public static IReadOnlyList<Span> HorizontalSpans(IReadOnlyList<Point2D> polygon, double y)
        => AxisSpans(polygon, y, horizontal: true);

    /// <summary>
    /// Intersects a vertical line (constant X) with the polygon and returns the interior spans in Y.
    /// </summary>
    public static IReadOnlyList<Span> VerticalSpans(IReadOnlyList<Point2D> polygon, double x)
        => AxisSpans(polygon, x, horizontal: false);

    private static IReadOnlyList<Span> AxisSpans(IReadOnlyList<Point2D> polygon, double coordinate, bool horizontal)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var crossings = new List<double>();

        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];

            // For horizontal scan, the varying edge coordinate is Y; the produced value is X.
            var a1 = horizontal ? a.Y : a.X;
            var b1 = horizontal ? b.Y : b.X;
            var a2 = horizontal ? a.X : a.Y;
            var b2 = horizontal ? b.X : b.Y;

            if (a1 == b1)
            {
                continue; // Parallel edge – skip to avoid duplicate/degenerate crossings.
            }

            if ((coordinate >= a1 && coordinate < b1) || (coordinate >= b1 && coordinate < a1))
            {
                var t = (coordinate - a1) / (b1 - a1);
                crossings.Add(a2 + (t * (b2 - a2)));
            }
        }

        crossings.Sort();

        var spans = new List<Span>();
        for (var i = 0; i + 1 < crossings.Count; i += 2)
        {
            var start = crossings[i];
            var end = crossings[i + 1];
            if (end > start)
            {
                spans.Add(new Span(start, end));
            }
        }

        return spans;
    }
}

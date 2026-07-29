namespace DeckDesigner.Domain.Geometry;

/// <summary>
/// Stateless helpers for working with 2D polygons.
/// </summary>
public static class PolygonMath
{
    /// <summary>Computes the absolute polygon area using the shoelace formula.</summary>
    public static double Area(IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(sum) / 2.0;
    }

    /// <summary>Signed area; positive when the winding is counter-clockwise.</summary>
    public static double SignedArea(IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            sum += (current.X * next.Y) - (next.X * current.Y);
        }

        return sum / 2.0;
    }

    public static Point2D Centroid(IReadOnlyList<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return Point2D.Origin;
        }

        double x = 0, y = 0;
        foreach (var p in points)
        {
            x += p.X;
            y += p.Y;
        }

        return new Point2D(x / points.Count, y / points.Count);
    }

    /// <summary>Ray-casting point-in-polygon test (boundary inclusive is not guaranteed).</summary>
    public static bool Contains(IReadOnlyList<Point2D> polygon, Point2D point)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (polygon.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                (point.X < ((pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y)) + pi.X))
            {
                inside = !inside;
            }
        }

        return inside;
    }
}

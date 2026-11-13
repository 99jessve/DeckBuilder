namespace DeckDesigner.Domain.Geometry;

using System.Linq;

/// <summary>
/// Small analytic-geometry helpers used by geometry generation: infinite-line
/// intersection, signed side-of-line tests and half-plane segment clipping.
/// All coordinates are in millimeters.
/// </summary>
public static class GeometryHelpers
{
    /// <summary>
    /// A directed line through <see cref="Point"/> with unit <see cref="Direction"/>.
    /// The inward normal is <c>Direction.Perpendicular()</c>.
    /// </summary>
    public readonly record struct Line(Point2D Point, Vector2D Direction);

    /// <summary>Builds a line through two points; direction is normalized.</summary>
    public static Line LineThrough(Point2D a, Point2D b)
    {
        var dir = new Vector2D(b.X - a.X, b.Y - a.Y).Normalized();
        return new Line(a, dir);
    }

    /// <summary>
    /// Signed distance of <paramref name="p"/> from <paramref name="line"/> along the
    /// line's left normal (Direction.Perpendicular()). Positive is to the left.
    /// </summary>
    public static double SignedDistance(Line line, Point2D p)
    {
        var n = line.Direction.Perpendicular();
        return ((p.X - line.Point.X) * n.X) + ((p.Y - line.Point.Y) * n.Y);
    }

    /// <summary>
    /// Intersection of two infinite lines. Returns null if (near) parallel.
    /// </summary>
    public static Point2D? Intersect(Line a, Line b)
    {
        var denom = (a.Direction.X * b.Direction.Y) - (a.Direction.Y * b.Direction.X);
        if (Math.Abs(denom) < 1e-9)
        {
            return null;
        }

        var dx = b.Point.X - a.Point.X;
        var dy = b.Point.Y - a.Point.Y;
        var t = ((dx * b.Direction.Y) - (dy * b.Direction.X)) / denom;
        return new Point2D(a.Point.X + (t * a.Direction.X), a.Point.Y + (t * a.Direction.Y));
    }

    /// <summary>
    /// Clips the segment (<paramref name="start"/>,<paramref name="end"/>) to the half-plane
    /// on the inward side of <paramref name="line"/> (the side <paramref name="inside"/> is on).
    /// Returns false if the segment lies entirely outside the half-plane.
    /// </summary>
    public static bool ClipSegmentToHalfPlane(
        Line line,
        Point2D inside,
        ref Point2D start,
        ref Point2D end)
    {
        var insideSign = Math.Sign(SignedDistance(line, inside));
        if (insideSign == 0)
        {
            insideSign = 1;
        }

        var ds = SignedDistance(line, start) * insideSign;
        var de = SignedDistance(line, end) * insideSign;

        var startInside = ds >= -1e-9;
        var endInside = de >= -1e-9;

        if (startInside && endInside)
        {
            return true;
        }

        if (!startInside && !endInside)
        {
            return false;
        }

        // One endpoint is outside; move it to the intersection point.
        var t = ds / (ds - de);
        var cut = new Point2D(
            start.X + (t * (end.X - start.X)),
            start.Y + (t * (end.Y - start.Y)));

        if (!startInside)
        {
            start = cut;
        }
        else
        {
            end = cut;
        }

        return true;
    }

    /// <summary>
    /// Clips a convex polygon to the half-plane on the inward side of <paramref name="line"/>
    /// (the side <paramref name="inside"/> is on) using Sutherland-Hodgman. Returns the clipped
    /// polygon vertices, or an empty list if nothing remains.
    /// </summary>
    public static List<Point2D> ClipPolygonToHalfPlane(
        IReadOnlyList<Point2D> polygon,
        Line line,
        Point2D inside)
    {
        var result = new List<Point2D>(polygon.Count + 1);
        if (polygon.Count == 0)
        {
            return result;
        }

        var insideSign = Math.Sign(SignedDistance(line, inside));
        if (insideSign == 0)
        {
            insideSign = 1;
        }

        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];

            var dc = SignedDistance(line, current) * insideSign;
            var dn = SignedDistance(line, next) * insideSign;

            var currentInside = dc >= -1e-9;
            var nextInside = dn >= -1e-9;

            if (currentInside)
            {
                result.Add(current);
            }

            // Edge crosses the clip line: add the intersection point.
            if (currentInside != nextInside)
            {
                var t = dc / (dc - dn);
                result.Add(new Point2D(
                    current.X + (t * (next.X - current.X)),
                    current.Y + (t * (next.Y - current.Y))));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns true if the polygon is simple (no non-adjacent edges intersect). Used to
    /// detect degenerate self-intersecting (bowtie) results from clipping against a
    /// near-degenerate region.
    /// </summary>
    public static bool IsSimplePolygon(IReadOnlyList<Point2D> polygon)
    {
        var n = polygon.Count;
        if (n < 4)
        {
            return true;
        }

        for (var i = 0; i < n; i++)
        {
            var a1 = polygon[i];
            var a2 = polygon[(i + 1) % n];
            for (var j = i + 1; j < n; j++)
            {
                // Skip adjacent edges (they share a vertex).
                if (j == i || (j + 1) % n == i || (i + 1) % n == j)
                {
                    continue;
                }

                var b1 = polygon[j];
                var b2 = polygon[(j + 1) % n];
                if (SegmentsProperlyIntersect(a1, a2, b1, b2))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Computes the convex hull (counter-clockwise) of a point set using the monotone
    /// chain algorithm. Used to repair a self-intersecting board footprint into a clean
    /// convex polygon.
    /// </summary>
    public static List<Point2D> ConvexHull(IReadOnlyList<Point2D> points)
    {
        var pts = points.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3)
        {
            return new List<Point2D>(pts);
        }

        var hull = new List<Point2D>();

        // Lower hull.
        foreach (var p in pts)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(p);
        }

        // Upper hull.
        var lower = hull.Count + 1;
        for (var i = pts.Count - 2; i >= 0; i--)
        {
            var p = pts[i];
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], p) <= 0)
            {
                hull.RemoveAt(hull.Count - 1);
            }

            hull.Add(p);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static double Cross(Point2D o, Point2D a, Point2D b)
        => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));

    private static bool SegmentsProperlyIntersect(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
    {
        var d1 = Cross(p3, p4, p1);
        var d2 = Cross(p3, p4, p2);
        var d3 = Cross(p1, p2, p3);
        var d4 = Cross(p1, p2, p4);

        return ((d1 > 1e-9 && d2 < -1e-9) || (d1 < -1e-9 && d2 > 1e-9))
            && ((d3 > 1e-9 && d4 < -1e-9) || (d3 < -1e-9 && d4 > 1e-9));
    }
}

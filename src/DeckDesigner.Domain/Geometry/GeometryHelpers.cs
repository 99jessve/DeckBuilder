namespace DeckDesigner.Domain.Geometry;

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
}

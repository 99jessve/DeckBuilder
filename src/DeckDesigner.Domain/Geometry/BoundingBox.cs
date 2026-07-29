namespace DeckDesigner.Domain.Geometry;

/// <summary>
/// An axis-aligned bounding box expressed in millimeters.
/// </summary>
public readonly record struct BoundingBox(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;

    public Point2D Min => new(MinX, MinY);

    public Point2D Max => new(MaxX, MaxY);

    public Point2D Center => new((MinX + MaxX) / 2.0, (MinY + MaxY) / 2.0);

    public bool Contains(Point2D point) =>
        point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

    public static BoundingBox FromPoints(IEnumerable<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var any = false;

        foreach (var p in points)
        {
            any = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        if (!any)
        {
            throw new ArgumentException("At least one point is required to build a bounding box.", nameof(points));
        }

        return new BoundingBox(minX, minY, maxX, maxY);
    }
}

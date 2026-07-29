namespace DeckDesigner.Domain.Geometry;

/// <summary>
/// An immutable 2D point expressed in millimeters.
/// </summary>
public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Origin => new(0, 0);

    public double DistanceTo(Point2D other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public Point2D Translate(Vector2D offset) => new(X + offset.X, Y + offset.Y);

    /// <summary>Rotates the point around <paramref name="center"/> by the given angle in radians.</summary>
    public Point2D Rotate(double radians, Point2D center)
    {
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = X - center.X;
        var dy = Y - center.Y;
        return new Point2D(
            center.X + ((dx * cos) - (dy * sin)),
            center.Y + ((dx * sin) + (dy * cos)));
    }

    public static Vector2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);

    public static Point2D operator +(Point2D p, Vector2D v) => new(p.X + v.X, p.Y + v.Y);
}

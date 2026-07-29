namespace DeckDesigner.Domain.Geometry;

/// <summary>
/// An immutable 2D vector expressed in millimeters.
/// </summary>
public readonly record struct Vector2D(double X, double Y)
{
    public static Vector2D Zero => new(0, 0);

    public double Length => Math.Sqrt((X * X) + (Y * Y));

    public double LengthSquared => (X * X) + (Y * Y);

    public Vector2D Normalized()
    {
        var length = Length;
        return length <= double.Epsilon ? Zero : new Vector2D(X / length, Y / length);
    }

    public double Dot(Vector2D other) => (X * other.X) + (Y * other.Y);

    /// <summary>Returns a vector rotated 90 degrees counter-clockwise.</summary>
    public Vector2D Perpendicular() => new(-Y, X);

    public Vector2D Rotate(double radians)
    {
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Vector2D((X * cos) - (Y * sin), (X * sin) + (Y * cos));
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2D operator *(Vector2D v, double scalar) => new(v.X * scalar, v.Y * scalar);

    public static Vector2D operator *(double scalar, Vector2D v) => v * scalar;

    public static Vector2D operator -(Vector2D v) => new(-v.X, -v.Y);
}

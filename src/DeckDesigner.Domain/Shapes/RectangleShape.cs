namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// An axis-aligned rectangular deck outline defined by an origin, width and height (mm).
/// </summary>
public sealed class RectangleShape : ShapeBase
{
    public RectangleShape(Point2D origin, double width, double height)
        : base(BuildCorners(origin, width, height))
    {
    }

    private static IEnumerable<Point2D> BuildCorners(Point2D origin, double width, double height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        }

        yield return origin;
        yield return new Point2D(origin.X + width, origin.Y);
        yield return new Point2D(origin.X + width, origin.Y + height);
        yield return new Point2D(origin.X, origin.Y + height);
    }
}

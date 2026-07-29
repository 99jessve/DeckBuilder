namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A triangular deck outline defined by three points (mm).
/// </summary>
public sealed class TriangleShape : ShapeBase
{
    public TriangleShape(Point2D a, Point2D b, Point2D c)
        : base(new[] { a, b, c })
    {
    }
}

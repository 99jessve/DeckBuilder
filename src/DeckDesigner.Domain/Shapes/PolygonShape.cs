namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// An arbitrary closed polygon deck outline (mm).
/// </summary>
public sealed class PolygonShape : ShapeBase
{
    public PolygonShape(IEnumerable<Point2D> points)
        : base(points)
    {
    }
}

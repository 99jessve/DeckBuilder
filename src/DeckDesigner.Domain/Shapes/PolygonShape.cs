namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// An arbitrary closed polygon deck outline (mm).
/// </summary>
public sealed class PolygonShape : ShapeBase
{
    public PolygonShape(IEnumerable<Point2D> points, IEnumerable<IReadOnlyList<Point2D>>? holes = null)
        : base(points)
    {
        Holes = holes?.Select(h => (IReadOnlyList<Point2D>)h.ToArray()).ToArray()
            ?? Array.Empty<IReadOnlyList<Point2D>>();
    }

    public IReadOnlyList<IReadOnlyList<Point2D>> Holes { get; private set; }

    public override double Area => base.Area - Holes.Sum(PolygonMath.Area);

    public override void Move(Vector2D offset)
    {
        base.Move(offset);
        Holes = Holes.Select(h => (IReadOnlyList<Point2D>)h.Select(p => p.Translate(offset)).ToArray()).ToArray();
    }

    public override void Rotate(double angle)
    {
        var center = PolygonMath.Centroid(Points);
        base.Rotate(angle);
        Holes = Holes.Select(h => (IReadOnlyList<Point2D>)h.Select(p => p.Rotate(angle, center)).ToArray()).ToArray();
    }
}

namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// Base implementation for polygon-backed shapes.
/// </summary>
public abstract class ShapeBase : IShape
{
    protected ShapeBase(IEnumerable<Point2D> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Vertices = points.ToList();
        if (Vertices.Count < 3)
        {
            throw new ArgumentException("A shape requires at least three points.", nameof(points));
        }
    }

    protected List<Point2D> Vertices { get; }

    public virtual double Area => PolygonMath.Area(Vertices);

    public IReadOnlyList<Point2D> Points => Vertices;

    public virtual void Move(Vector2D offset)
    {
        for (var i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] = Vertices[i].Translate(offset);
        }
    }

    public virtual void Rotate(double angle)
    {
        var center = PolygonMath.Centroid(Vertices);
        for (var i = 0; i < Vertices.Count; i++)
        {
            Vertices[i] = Vertices[i].Rotate(angle, center);
        }
    }

    public BoundingBox GetBounds() => BoundingBox.FromPoints(Vertices);
}

namespace DeckDesigner.Domain.Shapes;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A planar deck outline. All coordinates are in millimeters.
/// </summary>
public interface IShape
{
    double Area { get; }

    IReadOnlyList<Point2D> Points { get; }

    void Move(Vector2D offset);

    void Rotate(double angle);

    BoundingBox GetBounds();
}

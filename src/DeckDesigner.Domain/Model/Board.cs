namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A single decking board segment (mm).
/// </summary>
public sealed record Board(Point2D Start, Point2D End, double Width, bool NeedsJoint)
{
    public double Length => Start.DistanceTo(End);
}

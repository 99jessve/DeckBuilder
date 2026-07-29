namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A framing joist (mm).
/// </summary>
public sealed record Joist(Point2D Start, Point2D End, double Width, double Height)
{
    public double Length => Start.DistanceTo(End);
}

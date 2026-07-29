namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A supporting beam (mm). Reserved for future structural generation.
/// </summary>
public sealed record Beam(Point2D Start, Point2D End, double Width, double Height)
{
    public double Length => Start.DistanceTo(End);
}

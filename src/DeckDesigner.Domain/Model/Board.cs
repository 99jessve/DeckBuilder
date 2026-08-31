namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A single decking board segment (mm). <see cref="Corners"/>, when set, holds the
/// board's true mitred outline (in order) so it can be drawn with angled cut ends
/// instead of a square-capped stroke.
/// </summary>
public sealed record Board(Point2D Start, Point2D End, double Width, bool NeedsJoint, IReadOnlyList<Point2D>? Corners = null)
{
    public double Length => Start.DistanceTo(End);
}

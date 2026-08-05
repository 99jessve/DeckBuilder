namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A butt joint where two decking boards meet end-to-end. Industry practice requires
/// the joint to be centered over a joist for bearing support, and staggered relative
/// to neighbouring rows. The <see cref="Start"/>/<see cref="End"/> pair describes the
/// visible joint line drawn across the width of the board, and <see cref="Width"/> is
/// the plank width so it renders as a real crossing board.
/// </summary>
public sealed record ButtJoint(Point2D Start, Point2D End, double Width)
{
    /// <summary>Center of the joint line (the point sitting over the joist).</summary>
    public Point2D Center => new((Start.X + End.X) / 2.0, (Start.Y + End.Y) / 2.0);
}

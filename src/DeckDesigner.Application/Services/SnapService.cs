namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Computes edge-alignment snap offsets for a moving bounding box relative to
/// other bounding boxes. Enables shapes to snap together when edges are close.
/// </summary>
public sealed class SnapService
{
    /// <summary>
    /// Returns the offset that should be added to <paramref name="moving"/> so its
    /// nearest X and Y edges align with a neighbouring box, when within <paramref name="threshold"/>.
    /// Returns <see cref="Vector2D.Zero"/> when nothing is close enough.
    /// </summary>
    public Vector2D ComputeSnap(BoundingBox moving, IEnumerable<BoundingBox> others, double threshold)
    {
        ArgumentNullException.ThrowIfNull(others);
        return ComputeSnap(ToShape(moving), others.Select(ToShape), threshold);
    }

    public Vector2D ComputeSnap(IShape moving, IEnumerable<IShape> others, double threshold)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(others);
        if (!double.IsFinite(threshold) || threshold <= 0) return Vector2D.Zero;
        return others.SelectMany(other => EdgeContact.Candidates(moving, other, threshold))
            .Where(v => v.LengthSquared > 1e-12)
            .OrderBy(v => v.LengthSquared).FirstOrDefault();
    }

    private static IShape ToShape(BoundingBox box) => new RectangleShape(new(box.MinX, box.MinY), box.Width, box.Height);
}

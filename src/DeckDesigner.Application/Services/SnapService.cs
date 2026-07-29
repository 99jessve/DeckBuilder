namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;

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
        if (threshold <= 0)
        {
            return Vector2D.Zero;
        }

        var movingX = new[] { moving.MinX, moving.MaxX };
        var movingY = new[] { moving.MinY, moving.MaxY };

        var bestDx = 0.0;
        var bestDxDist = threshold;
        var bestDy = 0.0;
        var bestDyDist = threshold;

        foreach (var other in others)
        {
            var otherX = new[] { other.MinX, other.MaxX };
            var otherY = new[] { other.MinY, other.MaxY };

            // Only snap in X when the boxes overlap (or are near) in Y, and vice versa,
            // so distant shapes do not attract each other.
            var yNear = Overlaps(moving.MinY, moving.MaxY, other.MinY, other.MaxY, threshold);
            var xNear = Overlaps(moving.MinX, moving.MaxX, other.MinX, other.MaxX, threshold);

            if (yNear)
            {
                foreach (var m in movingX)
                {
                    foreach (var o in otherX)
                    {
                        var delta = o - m;
                        var dist = Math.Abs(delta);
                        if (dist < bestDxDist)
                        {
                            bestDxDist = dist;
                            bestDx = delta;
                        }
                    }
                }
            }

            if (xNear)
            {
                foreach (var m in movingY)
                {
                    foreach (var o in otherY)
                    {
                        var delta = o - m;
                        var dist = Math.Abs(delta);
                        if (dist < bestDyDist)
                        {
                            bestDyDist = dist;
                            bestDy = delta;
                        }
                    }
                }
            }
        }

        return new Vector2D(bestDx, bestDy);
    }

    private static bool Overlaps(double aMin, double aMax, double bMin, double bMax, double tolerance)
        => aMin <= bMax + tolerance && bMin <= aMax + tolerance;
}

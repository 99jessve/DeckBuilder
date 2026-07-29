namespace DeckDesigner.Tests;

using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;

public class SnapServiceTests
{
    [Fact]
    public void ComputeSnap_AlignsNearEdgesWithinThreshold()
    {
        var snap = new SnapService();

        // Moving box's right edge (990) is 10mm from the other's left edge (1000), overlapping in Y.
        var moving = new BoundingBox(0, 0, 990, 500);
        var other = new BoundingBox(1000, 0, 2000, 500);

        var offset = snap.ComputeSnap(moving, new[] { other }, threshold: 50);

        Assert.Equal(10, offset.X, 3);
        Assert.Equal(0, offset.Y, 3);
    }

    [Fact]
    public void ComputeSnap_ReturnsZeroWhenOutsideThreshold()
    {
        var snap = new SnapService();
        var moving = new BoundingBox(0, 0, 900, 500);
        var other = new BoundingBox(1000, 0, 2000, 500);

        var offset = snap.ComputeSnap(moving, new[] { other }, threshold: 50);

        Assert.Equal(0, offset.X, 3);
        Assert.Equal(0, offset.Y, 3);
    }

    [Fact]
    public void ComputeSnap_DoesNotAttractDistantShapes()
    {
        var snap = new SnapService();

        // Aligned in X edges but far apart in Y (no Y overlap) -> no X snap.
        var moving = new BoundingBox(0, 0, 990, 100);
        var other = new BoundingBox(1000, 5000, 2000, 5100);

        var offset = snap.ComputeSnap(moving, new[] { other }, threshold: 20);

        Assert.Equal(0, offset.X, 3);
        Assert.Equal(0, offset.Y, 3);
    }

    [Fact]
    public void ComputeSnap_SnapsTopEdgeToNeighbour()
    {
        var snap = new SnapService();

        // Moving box overlaps in X; its top edge (505) is 5mm from other's top edge (500).
        var moving = new BoundingBox(0, 505, 1000, 1000);
        var other = new BoundingBox(0, 500, 1000, 900);

        var offset = snap.ComputeSnap(moving, new[] { other }, threshold: 20);

        Assert.Equal(-5, offset.Y, 3);
    }
}

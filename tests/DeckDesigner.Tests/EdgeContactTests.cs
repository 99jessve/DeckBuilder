namespace DeckDesigner.Tests;

using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class EdgeContactTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(99, 2)]
    [InlineData(100, 1)]
    [InlineData(500, 1)]
    public void Composition_RequiresMinimumEdge(double overlap, int count)
    {
        var decks = new DeckCompositionService().Compose(DesignFixtures.Contact(overlap), new DeckSettings());
        Assert.Equal(count, decks.Count);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 10)]
    public void Snapping_RequiresMinimumEdge(double overlap, double dx)
    {
        var shapes = DesignFixtures.Contact(overlap);
        shapes[0].Move(new(-10, 0));
        Assert.Equal(dx, new SnapService().ComputeSnap(shapes[0], new[] { shapes[1] }, 20).X, 6);
    }

    [Fact]
    public void AngledEdges_SnapPerpendicularToEdge()
    {
        var a = new TriangleShape(new(0, 0), new(2000, 0), new(0, 2000));
        var b = new TriangleShape(new(2010, 10), new(2010, 2010), new(10, 2010));
        var offset = new SnapService().ComputeSnap(a, new[] { b }, 30);
        Assert.Equal(10, offset.X, 6);
        Assert.Equal(10, offset.Y, 6);
    }
}

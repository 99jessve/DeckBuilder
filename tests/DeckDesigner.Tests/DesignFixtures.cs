namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

internal static class DesignFixtures
{
    public static IShape Rectangle() => new RectangleShape(Point2D.Origin, 9000, 3000);

    public static IShape Triangle() => new TriangleShape(new(0, 0), new(9000, 0), new(0, 6000));

    public static IShape Concave() => new PolygonShape(new Point2D[]
    {
        new(0, 0), new(6000, 0), new(6000, 1500), new(2500, 1500), new(2500, 4000), new(0, 4000),
    });

    public static IShape[] Contact(double overlap) => new IShape[]
    {
        new RectangleShape(new(0, 0), 4000, 3000),
        new RectangleShape(new(4000, 3000 - overlap), 3000, 2000),
    };
}

public class DesignFixtureTests
{
    [Fact]
    public void Rebuild_IsDeterministic()
    {
        foreach (var shape in new[] { DesignFixtures.Rectangle(), DesignFixtures.Triangle(), DesignFixtures.Concave() })
        {
            var deck = new Deck(shape);
            deck.RebuildGeometry();
            var first = deck.Boards.Select(b => (b.Start, b.End, b.Width)).ToArray();
            deck.RebuildGeometry();
            Assert.Equal(first, deck.Boards.Select(b => (b.Start, b.End, b.Width)).ToArray());
            Assert.All(deck.Boards, b => Assert.True(double.IsFinite(b.Length) && b.Length > 0));
        }
    }
}

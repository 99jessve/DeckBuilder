namespace DeckDesigner.Tests;

using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class DeckCompositionServiceTests
{
    [Fact]
    public void Compose_TouchingShapes_MergeIntoSingleDeck()
    {
        var service = new DeckCompositionService();

        // Two adjacent rectangles sharing an edge at x = 1000.
        var left = new RectangleShape(new Point2D(0, 0), 1000, 500);
        var right = new RectangleShape(new Point2D(1000, 0), 1000, 500);

        var decks = service.Compose(new IShape[] { left, right }, new DeckSettings());

        Assert.Single(decks);
        Assert.Equal(1_000_000, decks[0].Shape.Area, 0);
    }

    [Fact]
    public void Compose_SeparateShapes_ProduceMultipleDecks()
    {
        var service = new DeckCompositionService();

        var a = new RectangleShape(new Point2D(0, 0), 1000, 500);
        var b = new RectangleShape(new Point2D(3000, 0), 1000, 500);

        var decks = service.Compose(new IShape[] { a, b }, new DeckSettings());

        Assert.Equal(2, decks.Count);
    }

    [Fact]
    public void Compose_ShapesWithinMergeTolerance_MergeIntoSingleDeck()
    {
        var service = new DeckCompositionService();

        // A sub-millimeter gap (as left after snapping) should still merge into one deck.
        var left = new RectangleShape(new Point2D(0, 0), 1000, 500);
        var right = new RectangleShape(new Point2D(1000.5, 0), 1000, 500);

        var decks = service.Compose(new IShape[] { left, right }, new DeckSettings());

        Assert.Single(decks);
    }

    [Fact]
    public void Compose_EmptyInput_ReturnsEmpty()
    {
        var service = new DeckCompositionService();

        var decks = service.Compose(Array.Empty<IShape>(), new DeckSettings());

        Assert.Empty(decks);
    }

    [Fact]
    public void Compose_MergedDeck_HasRebuiltGeometry()
    {
        var service = new DeckCompositionService();
        var shape = new RectangleShape(new Point2D(0, 0), 2000, 2000);

        var decks = service.Compose(new IShape[] { shape }, new DeckSettings());

        Assert.Single(decks);
        Assert.NotEmpty(decks[0].Boards);
    }

    [Fact]
    public void Compose_TriangleCornerOnRectangleSide_ProducesSimpleBoardPolygons()
    {
        var service = new DeckCompositionService();

        // A triangle whose corner overlaps the rectangle's right side, producing a merged
        // outline with a thin neck. Clipping field boards against the near-degenerate
        // interior there used to yield self-intersecting / pinched (duplicate-vertex)
        // polygons that rendered as dark holes. Every board footprint must be a clean
        // simple polygon with no coincident vertices.
        var rect = new RectangleShape(new Point2D(0, 0), 4000, 3000);
        var tri = new TriangleShape(
            new Point2D(3950, 1400),
            new Point2D(7000, 1400),
            new Point2D(7000, 4400));

        var decks = service.Compose(new IShape[] { rect, tri }, new DeckSettings());

        Assert.Single(decks);
        foreach (var board in decks[0].Boards)
        {
            if (board.Corners is not { Count: >= 3 } corners)
            {
                continue;
            }

            Assert.True(GeometryHelpers.IsSimplePolygon(corners), "Board polygon is self-intersecting.");

            for (var i = 0; i < corners.Count; i++)
            {
                for (var j = i + 1; j < corners.Count; j++)
                {
                    var dup = Math.Abs(corners[i].X - corners[j].X) < 1e-6
                        && Math.Abs(corners[i].Y - corners[j].Y) < 1e-6;
                    Assert.False(dup, "Board polygon has a duplicate (pinched) vertex.");
                }
            }
        }
    }
}

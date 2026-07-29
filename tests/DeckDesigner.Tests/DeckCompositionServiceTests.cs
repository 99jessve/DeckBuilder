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
}

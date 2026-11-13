namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using DeckDesigner.Infrastructure.Persistence;

public class DesignPersistenceTests
{
    [Fact]
    public void AuthoredInputs_RoundTrip()
    {
        var deck = new Deck(new RectangleShape(Point2D.Origin, 4000, 3000)) { AutomaticDividers = false };
        deck.Dividers.Add(new("middle", BoardDirection.Vertical, 2000));
        deck.SectionDirections["root/middle:before"] = BoardDirection.Vertical;
        var repository = new DeckProjectRepository();
        var loaded = Assert.Single(repository.Deserialize(repository.Serialize(new[] { deck })));
        Assert.Equal(deck.Id, loaded.Id);
        Assert.Equal(ShapeConstraint.Rectangle, loaded.Constraint);
        Assert.False(loaded.AutomaticDividers);
        Assert.Equal(deck.Dividers, loaded.Dividers);
        Assert.Equal(deck.SectionDirections, loaded.SectionDirections);
        Assert.Equal(BoardDirection.Vertical, loaded.Sections.Single(s => s.Id == "root/middle:before").Direction);
        Assert.Equal(2, loaded.Sections.Count);
    }

    [Fact]
    public void LegacyPolygon_DoesNotGuessConstraints()
    {
        var loaded = new DeckProjectRepository().Deserialize("""
            {"version":1,"decks":[{"shapePoints":[{"x":0,"y":0},{"x":3000,"y":0},{"x":0,"y":2000}],
            "settings":{"boardWidth":145,"boardThickness":28,"boardGap":5,"maxBoardLength":5400,"joistSpacing":400,"joistWidth":45,"joistHeight":145}}]}
            """);
        Assert.Equal(ShapeConstraint.FreePolygon, Assert.Single(loaded).Constraint);
    }

    [Fact]
    public void FutureVersion_IsRejected() => Assert.Throws<InvalidOperationException>(
        () => new DeckProjectRepository().Deserialize("{\"version\":99,\"decks\":[]}"));
}

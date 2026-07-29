namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using DeckDesigner.Infrastructure.Persistence;

public class PersistenceTests
{
    [Fact]
    public void RoundTrip_PreservesShapeAndSettings()
    {
        var settings = new DeckSettings { BoardWidth = 120, JoistSpacing = 500 };
        var metadata = new DeckMetadata { Name = "Patio" };
        var deck = new Deck(new RectangleShape(Point2D.Origin, 4000, 3000), settings, metadata);
        deck.RebuildGeometry();

        var repo = new DeckProjectRepository();
        var json = repo.Serialize(new[] { deck });
        var restored = repo.Deserialize(json);

        var loaded = Assert.Single(restored);
        Assert.Equal("Patio", loaded.Metadata.Name);
        Assert.Equal(120, loaded.Settings.BoardWidth, 3);
        Assert.Equal(500, loaded.Settings.JoistSpacing, 3);
        Assert.Equal(deck.Shape.Points.Count, loaded.Shape.Points.Count);
        Assert.NotEmpty(loaded.Boards);
    }
}

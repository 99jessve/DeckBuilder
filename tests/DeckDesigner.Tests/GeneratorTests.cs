namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class GeneratorTests
{
    private static Deck BuildDeck(DeckSettings? settings = null)
    {
        var shape = new RectangleShape(Point2D.Origin, 4000, 3000);
        var deck = new Deck(shape, settings ?? new DeckSettings());
        deck.RebuildGeometry();
        return deck;
    }

    [Fact]
    public void RebuildGeometry_GeneratesBoards()
    {
        var deck = BuildDeck();
        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();
        Assert.NotEmpty(fieldBoards);
        Assert.All(fieldBoards, b => Assert.Equal(4000, b.Length, 3));
    }

    [Fact]
    public void BoardRows_RespectPitch()
    {
        var settings = new DeckSettings { BoardWidth = 145, BoardGap = 5 };
        var deck = BuildDeck(settings);
        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();

        // Deck height 3000, pitch 150, first row at 72.5 => ~20 rows.
        Assert.Equal(20, fieldBoards.Count);
    }

    [Fact]
    public void LongSpan_FlagsNeedsJoint()
    {
        var shape = new RectangleShape(Point2D.Origin, 6000, 1000);
        var settings = new DeckSettings { MaxBoardLength = 5400 };
        var deck = new Deck(shape, settings);
        deck.RebuildGeometry();

        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();
        Assert.All(fieldBoards, b => Assert.True(b.NeedsJoint));
    }

    [Fact]
    public void RebuildGeometry_GeneratesJoists()
    {
        var deck = BuildDeck();
        Assert.NotEmpty(deck.Joists);
        Assert.All(deck.Joists, j => Assert.Equal(3000, j.Length, 3));
    }
}

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
        // Field boards are trimmed to butt against the two vertical cover boards
        // (one board width, 145mm, in from each side): 4000 - 2*145 = 3710.
        Assert.All(fieldBoards, b => Assert.Equal(3710, b.Length, 3));
    }

    [Fact]
    public void BoardRows_RespectPitch()
    {
        var settings = new DeckSettings { BoardWidth = 145, BoardGap = 5 };
        var deck = BuildDeck(settings);
        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();

        // Deck height 3000, pitch 150 => 18 full rows fit between the top and bottom
        // picture-frame borders, plus one narrower "cut" plank filling the leftover
        // strip against the bottom border => 19.
        Assert.Equal(19, fieldBoards.Count);
    }

    [Fact]
    public void LongSpan_ProducesStockLengthPiecesAndCenteredBreaker()
    {
        var shape = new RectangleShape(Point2D.Origin, 6000, 1000);
        var settings = new DeckSettings { MaxBoardLength = 5400 };
        var deck = new Deck(shape, settings);
        deck.RebuildGeometry();

        Assert.Equal(3000, Assert.Single(deck.EffectiveDividers).Position, 6);
        Assert.Equal(2, deck.Sections.Count);
        Assert.NotEmpty(deck.BreakerBoards);
        Assert.All(deck.Boards, b =>
        {
            Assert.False(b.NeedsJoint);
            Assert.InRange(b.Length, 0.001, settings.MaxBoardLength + 1e-6);
        });
    }

    [Fact]
    public void RebuildGeometry_GeneratesJoists()
    {
        var deck = BuildDeck();
        Assert.NotEmpty(deck.Joists);
        Assert.Contains(deck.Joists, j => Math.Abs(j.Length - 2710) < 1e-6);
        Assert.All(deck.Joists, j => Assert.True(j.Length > 0));
    }
}

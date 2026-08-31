namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Generation;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class EdgeBoardGeneratorTests
{
    [Fact]
    public void Rectangle_HorizontalBoards_CoversAllFourEdges()
    {
        var gen = new EdgeBoardGenerator();
        var settings = new DeckSettings { BoardDirection = BoardDirection.Horizontal };
        var shape = new RectangleShape(new Point2D(0, 0), 4000, 3000);

        var edges = gen.Generate(shape, settings);

        // A full picture-frame border puts a mitred cover board on every edge.
        Assert.Equal(4, edges.Count);
    }

    [Fact]
    public void Triangle_CoversHypotenuse()
    {
        var gen = new EdgeBoardGenerator();
        var settings = new DeckSettings { BoardDirection = BoardDirection.Horizontal };

        // Right-angled triangle with a horizontal leg, a vertical leg, and a hypotenuse.
        var shape = new TriangleShape(
            new Point2D(0, 0),
            new Point2D(4000, 0),
            new Point2D(0, 3000));

        var edges = gen.Generate(shape, settings);

        // A full picture-frame border puts a mitred cover board on every edge,
        // including the hypotenuse.
        Assert.Equal(3, edges.Count);
    }

    [Fact]
    public void EdgeBoards_AreIncludedInDeckBoards()
    {
        var shape = new RectangleShape(new Point2D(0, 0), 4000, 3000);
        var deck = new Deck(shape, new DeckSettings());
        deck.RebuildGeometry();

        Assert.NotEmpty(deck.EdgeBoards);
        Assert.All(deck.EdgeBoards, e => Assert.Contains(e, deck.Boards));
    }
}

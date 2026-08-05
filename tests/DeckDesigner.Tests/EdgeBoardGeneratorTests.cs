namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Generation;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class EdgeBoardGeneratorTests
{
    [Fact]
    public void Rectangle_HorizontalBoards_CoversTwoVerticalEdges()
    {
        var gen = new EdgeBoardGenerator();
        var settings = new DeckSettings { BoardDirection = BoardDirection.Horizontal };
        var shape = new RectangleShape(new Point2D(0, 0), 4000, 3000);

        var edges = gen.Generate(shape, settings);

        // The two vertical edges expose plank ends and need a cover board; the two
        // horizontal edges run parallel to the boards and do not.
        Assert.Equal(2, edges.Count);
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

        // The horizontal leg is parallel to the boards (no cover). The vertical leg and
        // the hypotenuse both expose ends and get a cover board.
        Assert.Equal(2, edges.Count);
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

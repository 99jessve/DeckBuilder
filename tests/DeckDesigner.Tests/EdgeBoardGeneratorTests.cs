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

    [Fact]
    public void ConcaveLShape_StillGeneratesFieldBoards()
    {
        // An L-shaped (concave) outline, like two snapped rectangles merged into one
        // deck. Its centroid falls outside the shape, which used to flip the inward
        // normals and clip away every field board.
        var lShape = new PolygonShape(new List<Point2D>
        {
            new(0, 0),
            new(4000, 0),
            new(4000, 1500),
            new(2000, 1500),
            new(2000, 3000),
            new(0, 3000),
        });

        var deck = new Deck(lShape, new DeckSettings());
        deck.RebuildGeometry();

        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();
        Assert.NotEmpty(fieldBoards);

        // The wide lower arm (full 4000 width) must still be filled: a reflex edge used to
        // slice across the interior and clip the wide boards down to a small convex core.
        Assert.Contains(fieldBoards, b => b.Length > 3000);
    }

    [Fact]
    public void ConcaveShape_DoesNotProduceFullLengthSlivers()
    {
        // A concave outline with a diagonal edge (like a rectangle merged with a triangle).
        // Intersecting every edge's infinite half-plane used to slice interior boards down
        // to hairline slivers that still spanned the full deck width. Clipping against the
        // real interior polygon must instead keep full-width planks.
        var shape = new PolygonShape(new List<Point2D>
        {
            new(0, 0),
            new(4000, 0),
            new(4000, 1200),
            new(6000, 1600),
            new(6000, 3600),
            new(2600, 3600),
            new(2600, 2000),
            new(0, 2000),
        });

        var deck = new Deck(shape, new DeckSettings());
        deck.RebuildGeometry();

        var fieldBoards = deck.Boards.Except(deck.EdgeBoards).ToList();
        Assert.NotEmpty(fieldBoards);

        // The bug produced hairline planks that still spanned the full deck width
        // (~5700mm). Clipping against the real interior polygon keeps such wide rows at
        // full board width instead of collapsing them.
        Assert.DoesNotContain(fieldBoards, b => b.Length > 4000 && b.Width < 100);
    }

    [Fact]
    public void AcuteJunction_EdgeBoardsStayWithinShapeBounds()
    {
        var gen = new EdgeBoardGenerator();
        var settings = new DeckSettings();

        // A shape with a very acute corner (thin neck), like the pinch where two snapped
        // shapes barely touch. An unbounded miter used to shoot a spike far outside the
        // outline at such junctions.
        var shape = new PolygonShape(new List<Point2D>
        {
            new(0, 0),
            new(4000, 0),
            new(4000, 2000),
            new(200, 2000),
            new(0, 1900),
        });

        var edges = gen.GenerateEdges(shape, settings);

        // Bounds with a generous margin (one board width). No mitred corner should extend
        // beyond the outline into a spike.
        const double margin = 200;
        foreach (var edge in edges)
        {
            foreach (var corner in edge.Board.Corners!)
            {
                Assert.InRange(corner.X, -margin, 4000 + margin);
                Assert.InRange(corner.Y, -margin, 2000 + margin);
            }
        }
    }
}

namespace DeckDesigner.Tests;

using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using DeckDesigner.Infrastructure.Persistence;

public class DeckEditingTests
{
    private readonly DeckEditingService _editor = new();

    private static Deck Rectangle()
    {
        var deck = new Deck(new RectangleShape(Point2D.Origin, 9000, 3000));
        deck.RebuildGeometry();
        return deck;
    }

    [Fact]
    public void VerticalDecking_UsesMovableHorizontalDividerAndBearing()
    {
        var deck = new Deck(new RectangleShape(Point2D.Origin, 3000, 9000),
            new DeckSettings { BoardDirection = BoardDirection.Vertical });
        deck.RebuildGeometry();
        Assert.Equal(BoardDirection.Horizontal, Assert.Single(deck.EffectiveDividers).Direction);
        var edited = _editor.MoveDivider(deck, "auto-1", 4200);
        Assert.Equal(4200, Assert.Single(edited.EffectiveDividers).Position);
        Assert.Contains(edited.Joists, j => Math.Abs(j.Start.Y - 4125) < 1e-6 &&
            Math.Abs(j.End.Y - 4125) < 1e-6 && j.Length >= 2710);
        Assert.All(edited.Sections.SelectMany(s => s.Boards), b => Assert.Equal(b.Start.X, b.End.X, 6));
    }

    [Fact]
    public void DividerCannotCrossAnotherSectionBoundary()
    {
        var deck = new Deck(new RectangleShape(Point2D.Origin, 4000, 3000)) { AutomaticDividers = false };
        deck.Dividers.Add(new("a", BoardDirection.Vertical, 1000));
        deck.Dividers.Add(new("b", BoardDirection.Vertical, 2000));
        deck.RebuildGeometry();
        Assert.Throws<InvalidOperationException>(() => _editor.MoveDivider(deck, "a", 2500));
    }

    [Theory]
    [InlineData(0, -1000, -500)]
    [InlineData(1, 10000, -500)]
    [InlineData(2, 10000, 3500)]
    [InlineData(3, -1000, 3500)]
    public void ResizeRectangle_KeepsOppositeCornerAndRightAngles(int corner, double x, double y)
    {
        var source = Rectangle();
        var edited = _editor.ResizeCorner(source, corner, new(x, y));
        var points = edited.Shape.Points;
        Assert.Equal(source.Shape.Points[(corner + 2) % 4], points[(corner + 2) % 4]);
        Assert.Equal(new Point2D(x, y), points[corner]);
        for (var i = 0; i < 4; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % 4];
            var c = points[(i + 2) % 4];
            Assert.Equal(0, new Vector2D(b.X - a.X, b.Y - a.Y).Dot(new(c.X - b.X, c.Y - b.Y)), 6);
        }
        Assert.Equal(9000, source.Shape.GetBounds().Width);
        Assert.Equal(ShapeConstraint.Rectangle, edited.Constraint);
    }

    [Fact]
    public void InvalidCornerAndDividerEdits_LeaveSourceUnchanged()
    {
        var source = Rectangle();
        var boards = source.Boards;
        Assert.Throws<InvalidOperationException>(() => _editor.ResizeCorner(source, 0, new(9100, 3100)));
        Assert.Throws<InvalidOperationException>(() => _editor.MoveDivider(source, "auto-1", 8000));
        Assert.Throws<InvalidOperationException>(() => _editor.MoveDivider(source, "auto-1", -100));
        Assert.Same(boards, source.Boards);
        Assert.True(source.AutomaticDividers);
        Assert.Empty(source.Dividers);
    }

    [Theory]
    [InlineData(0, -1000, 100)]
    [InlineData(1, 3500, -500)]
    [InlineData(2, 4100, 3500)]
    public void RightTriangle_ResizingPreservesRightAngle(int index, double x, double y)
    {
        var deck = new Deck(new TriangleShape(new(0, 0), new(4000, 0), new(4000, 3000)))
        {
            Constraint = ShapeConstraint.RightTriangle,
        };
        deck.RebuildGeometry();
        var edited = _editor.ResizeCorner(deck, index, new(x, y));
        var p = edited.Shape.Points;
        Assert.Equal(0, new Vector2D(p[0].X - p[1].X, p[0].Y - p[1].Y)
            .Dot(new(p[2].X - p[1].X, p[2].Y - p[1].Y)), 6);
    }

    [Fact]
    public void SelfIntersectingCornerEdit_IsRejected()
    {
        var deck = Rectangle();
        deck.Constraint = ShapeConstraint.FreePolygon;
        Assert.Throws<ArgumentException>(() => _editor.ResizeCorner(deck, 1, new(-1000, 1500)));
    }

    [Fact]
    public void DirectionEdit_ChangesOnlySelectedSectionAndSurvivesPersistence()
    {
        var source = Rectangle();
        var id = source.Sections[0].Id;
        var edited = _editor.SetSectionDirection(source, id, BoardDirection.Vertical);
        var repository = new DeckProjectRepository();
        var loaded = Assert.Single(repository.Deserialize(repository.Serialize(new[] { edited })));
        Assert.Equal(BoardDirection.Horizontal, source.Sections[0].Direction);
        Assert.Equal(BoardDirection.Vertical, loaded.Sections.Single(s => s.Id == id).Direction);
        Assert.All(loaded.Sections.Single(s => s.Id == id).Boards, b => Assert.Equal(b.Start.X, b.End.X, 6));
        Assert.Equal(BoardDirection.Horizontal, loaded.Sections.Single(s => s.Id != id).Direction);
        Assert.Contains(loaded.Joists, j => Math.Abs(j.Start.Y - j.End.Y) < 1e-6 && j.Length > 1000);
        Assert.Equal(edited.Boards.Select(b => (b.Start, b.End)), loaded.Boards.Select(b => (b.Start, b.End)));
    }

    [Fact]
    public void Translation_MovesAuthoredDividersWithShape()
    {
        var source = _editor.MoveDivider(Rectangle(), "auto-1", 4200);
        var moved = _editor.Translate(source, new(600, -200));
        Assert.Equal(4800, Assert.Single(moved.EffectiveDividers).Position);
        Assert.Equal(new Point2D(600, -200), moved.Shape.Points[0]);
        Assert.Equal(source.Sections.Select(s => s.Id), moved.Sections.Select(s => s.Id));
    }

    [Fact]
    public void HitTesting_DistinguishesCornersDividersSectionsAndOutside()
    {
        var deck = Rectangle();
        Assert.Equal(0, _editor.HitCorner(deck, new(3, 4), 8));
        Assert.Equal(-1, _editor.HitCorner(deck, new(30, 40), 8));
        Assert.Equal("auto-1", _editor.HitDivider(deck, new(4500, 1000), 10)?.Id);
        Assert.Null(_editor.HitDivider(deck, new(4500, -100), 10));
        Assert.Null(_editor.HitSection(deck, new(4500, 1000)));
        Assert.NotEqual(_editor.HitSection(deck, new(1000, 1000))?.Id,
            _editor.HitSection(deck, new(7000, 1000))?.Id);
    }
}

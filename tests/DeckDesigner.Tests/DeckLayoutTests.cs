namespace DeckDesigner.Tests;

using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using NetTopologySuite.Geometries;

public class DeckLayoutTests
{
    public static IEnumerable<object[]> Outlines()
    {
        yield return new object[] { DesignFixtures.Rectangle() };
        yield return new object[] { DesignFixtures.Triangle() };
        yield return new object[] { DesignFixtures.Concave() };
        yield return new object[] { new RectangleShape(Point2D.Origin, 18000, 8000) };
    }

    [Theory]
    [MemberData(nameof(Outlines))]
    public void PhysicalBoards_AreValidContainedAndWithinStock(IShape shape)
    {
        var deck = new Deck(shape);
        deck.RebuildGeometry();
        var outline = PolygonGeometry.ToPolygon(shape);
        Assert.NotEmpty(deck.Sections);
        Assert.Empty(deck.ButtJoints);
        Assert.All(deck.Boards, board =>
        {
            Assert.False(board.NeedsJoint);
            Assert.InRange(board.Length, 0.000001, 5400.000001);
            var footprint = PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(board.Corners!));
            Assert.True(footprint.IsValid);
            Assert.True(footprint.Difference(outline).Area < 1e-5);
        });
        var field = deck.Sections.SelectMany(s => s.Boards).ToArray();
        foreach (var board in field)
        {
            var footprint = PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(board.Corners!));
            foreach (var breaker in deck.BreakerBoards)
                Assert.True(footprint.Intersection(PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(breaker.Corners!))).Area < 1e-5);
        }
    }

    [Fact]
    public void Triangle_BreakerCrossesEntireInteriorInsteadOfAlternateRows()
    {
        var deck = new Deck(DesignFixtures.Triangle());
        deck.RebuildGeometry();
        var divider = Assert.Single(deck.EffectiveDividers);
        var interior = PolygonGeometry.Offset(PolygonGeometry.ToPolygon(deck.Shape), -deck.Settings.BoardWidth);
        var bounds = interior.EnvelopeInternal;
        Assert.Equal((bounds.MinX + bounds.MaxX) / 2, divider.Position, 6);
        var strip = PolygonGeometry.Box(divider.Position - 72.5, bounds.MinY - 1,
            divider.Position + 72.5, bounds.MaxY + 1);
        var expected = strip.Intersection(interior);
        var actual = PolygonGeometry.Factory.BuildGeometry(deck.BreakerBoards.Select(b =>
            (Geometry)PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(b.Corners!))).ToList()).Union();
        Assert.True(expected.SymmetricDifference(actual).Area < 1e-5);
    }

    [Fact]
    public void MovedDivider_HasBearingOnBothSidesAndStableSectionIds()
    {
        var deck = new Deck(DesignFixtures.Rectangle());
        deck.RebuildGeometry();
        var ids = deck.Sections.Select(s => s.Id).ToArray();
        var edited = new DeckEditingService().MoveDivider(deck, "auto-1", 4200);
        Assert.Equal(ids, edited.Sections.Select(s => s.Id));
        Assert.Equal(4500, deck.EffectiveDividers[0].Position);
        Assert.False(edited.AutomaticDividers);
        foreach (var x in new[] { 4125.0, 4275.0 })
        {
            Assert.Contains(edited.Joists, j => Math.Abs(j.Start.X - x) < 1e-6 &&
                Math.Abs(j.End.X - x) < 1e-6 && j.Start.Y <= 145 && j.End.Y >= 2855);
            Assert.Single(edited.Joists.Where(j => Math.Abs(j.Start.X - x) < 20 &&
                Math.Abs(j.End.X - x) < 20 && j.Length > 1000));
        }
    }

    [Fact]
    public void StockAvailability_DeterminesDividerCount()
    {
        var deck = new Deck(DesignFixtures.Rectangle(), new DeckSettings { StockLengths = new[] { 3000.0 } });
        deck.RebuildGeometry();
        Assert.Equal(2, deck.EffectiveDividers.Count);
        Assert.All(deck.Boards, b => Assert.InRange(b.Length, 0.000001, 3000.000001));
    }

    [Fact]
    public void FailedRebuild_DoesNotReplaceGeneratedGeometry()
    {
        var deck = new Deck(DesignFixtures.Rectangle());
        deck.RebuildGeometry();
        var boards = deck.Boards;
        var joists = deck.Joists;
        deck.AutomaticDividers = false;
        deck.Dividers.Add(new("outside", BoardDirection.Vertical, -10));
        Assert.Throws<InvalidOperationException>(deck.RebuildGeometry);
        Assert.Same(boards, deck.Boards);
        Assert.Same(joists, deck.Joists);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    public void InvalidSettings_AreRejected(double spacing)
    {
        var deck = new Deck(DesignFixtures.Rectangle(), new DeckSettings { JoistSpacing = spacing });
        Assert.Throws<ArgumentException>(deck.RebuildGeometry);
    }
}

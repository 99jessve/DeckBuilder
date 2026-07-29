namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Shapes;

public class GeometryTests
{
    [Fact]
    public void RectangleArea_IsWidthTimesHeight()
    {
        var rect = new RectangleShape(Point2D.Origin, 2000, 3000);
        Assert.Equal(6_000_000, rect.Area, 3);
    }

    [Fact]
    public void TriangleArea_UsesShoelace()
    {
        var tri = new TriangleShape(new Point2D(0, 0), new Point2D(4000, 0), new Point2D(0, 3000));
        Assert.Equal(6_000_000, tri.Area, 3);
    }

    [Fact]
    public void Move_TranslatesAllPoints()
    {
        var rect = new RectangleShape(Point2D.Origin, 1000, 1000);
        rect.Move(new Vector2D(500, 250));
        var bounds = rect.GetBounds();
        Assert.Equal(500, bounds.MinX, 3);
        Assert.Equal(250, bounds.MinY, 3);
        Assert.Equal(1500, bounds.MaxX, 3);
        Assert.Equal(1250, bounds.MaxY, 3);
    }

    [Fact]
    public void HorizontalSpans_ReturnSingleSpanForRectangle()
    {
        var rect = new RectangleShape(Point2D.Origin, 2000, 1000);
        var spans = ScanLine.HorizontalSpans(rect.Points, 500);
        var span = Assert.Single(spans);
        Assert.Equal(0, span.Start, 3);
        Assert.Equal(2000, span.End, 3);
    }
}

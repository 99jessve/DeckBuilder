namespace DeckDesigner.Tests;

using DeckDesigner.Application.Optimization;
using DeckDesigner.Application.Reporting;
using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class MaterialReportTests
{
    [Fact]
    public void Generate_ProducesConsistentReport()
    {
        var deck = new Deck(new RectangleShape(Point2D.Origin, 4000, 3000));
        var geometry = new GeometryService();
        geometry.Rebuild(deck);

        var service = new MaterialReportService(new CutOptimizer());
        var report = service.Generate(deck);

        Assert.Equal(deck.Boards.Count, report.BoardCount);
        Assert.Equal(12.0, report.AreaSquareMeters, 3);
        Assert.True(report.BoardRunningMillimeters > 0);
        Assert.True(report.StockBoardCount > 0);
        Assert.Equal(deck.Boards.Count * deck.Joists.Count * deck.Settings.ScrewsPerIntersection, report.ScrewCount);
    }
}

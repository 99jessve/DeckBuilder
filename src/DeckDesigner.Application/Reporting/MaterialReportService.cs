namespace DeckDesigner.Application.Reporting;

using DeckDesigner.Application.Optimization;
using DeckDesigner.Domain.Model;

/// <summary>
/// Builds a <see cref="MaterialReport"/> purely from domain geometry.
/// </summary>
public sealed class MaterialReportService(CutOptimizer optimizer)
{
    private readonly CutOptimizer _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));

    public MaterialReport Generate(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        var settings = deck.Settings;
        var boards = deck.Boards;
        var joists = deck.Joists;

        var boardRunning = boards.Sum(b => b.Length);
        var joistRunning = joists.Sum(j => j.Length);

        var boardLengths = boards.Select(b => b.Length);
        var cutPlan = _optimizer.Optimize(boardLengths, settings.StockLengths);

        // One intersection per board/joist crossing (approximation for screw count).
        var screwCount = boards.Count * joists.Count * settings.ScrewsPerIntersection;

        return new MaterialReport
        {
            AreaSquareMillimeters = deck.Shape.Area,
            BoardCount = boards.Count,
            BoardRunningMillimeters = boardRunning,
            JoistCount = joists.Count,
            JoistRunningMillimeters = joistRunning,
            BeamCount = deck.Beams.Count,
            PostCount = deck.Posts.Count,
            ScrewCount = screwCount,
            StockBoardCount = cutPlan.StockBoardCount,
            WasteMillimeters = cutPlan.TotalWaste,
            CutPlan = cutPlan,
        };
    }
}

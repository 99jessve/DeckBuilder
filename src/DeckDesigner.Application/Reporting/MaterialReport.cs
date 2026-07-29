namespace DeckDesigner.Application.Reporting;

using DeckDesigner.Application.Optimization;

/// <summary>
/// Aggregated bill of materials for a deck. All lengths in millimeters, area in square millimeters.
/// </summary>
public sealed record MaterialReport
{
    public required double AreaSquareMillimeters { get; init; }

    public double AreaSquareMeters => AreaSquareMillimeters / 1_000_000.0;

    public required int BoardCount { get; init; }

    public required double BoardRunningMillimeters { get; init; }

    public double BoardRunningMeters => BoardRunningMillimeters / 1000.0;

    public required int JoistCount { get; init; }

    public required double JoistRunningMillimeters { get; init; }

    public required int BeamCount { get; init; }

    public required int PostCount { get; init; }

    public required int ScrewCount { get; init; }

    public required int StockBoardCount { get; init; }

    public required double WasteMillimeters { get; init; }

    public double WasteMeters => WasteMillimeters / 1000.0;

    public required CutPlan CutPlan { get; init; }
}

namespace DeckDesigner.Domain.Model;

/// <summary>
/// Configurable rules that drive automatic geometry generation. All lengths in millimeters.
/// </summary>
public sealed record DeckSettings
{
    /// <summary>Width of a single decking board.</summary>
    public double BoardWidth { get; init; } = 145;

    /// <summary>Thickness/height of a decking board.</summary>
    public double BoardThickness { get; init; } = 28;

    /// <summary>Gap between adjacent decking boards.</summary>
    public double BoardGap { get; init; } = 5;

    /// <summary>Direction the decking boards run.</summary>
    public BoardDirection BoardDirection { get; init; } = BoardDirection.Horizontal;

    /// <summary>Maximum length of a single physical board before a joint is required.</summary>
    public double MaxBoardLength { get; init; } = 5400;

    /// <summary>Center-to-center spacing between joists.</summary>
    public double JoistSpacing { get; init; } = 400;

    /// <summary>Cross-section width of a joist.</summary>
    public double JoistWidth { get; init; } = 45;

    /// <summary>Cross-section height of a joist.</summary>
    public double JoistHeight { get; init; } = 145;

    /// <summary>Number of screws used per board/joist intersection.</summary>
    public int ScrewsPerIntersection { get; init; } = 2;

    /// <summary>Available stock lengths used for cut optimization.</summary>
    public IReadOnlyList<double> StockLengths { get; init; } = new double[] { 3000, 3600, 4200, 4800, 5400 };
}

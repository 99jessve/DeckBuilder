namespace DeckDesigner.Application.Optimization;

/// <summary>
/// A single stock board and the cuts assigned to it.
/// </summary>
public sealed record StockBoard(double StockLength, IReadOnlyList<double> Cuts)
{
    public double UsedLength => Cuts.Sum();

    public double Remaining => StockLength - UsedLength;
}

/// <summary>
/// The result of optimizing required board lengths onto stock boards.
/// </summary>
public sealed record CutPlan(IReadOnlyList<StockBoard> StockBoards)
{
    public int StockBoardCount => StockBoards.Count;

    public double TotalStockLength => StockBoards.Sum(b => b.StockLength);

    public double TotalUsedLength => StockBoards.Sum(b => b.UsedLength);

    public double TotalWaste => StockBoards.Sum(b => b.Remaining);
}

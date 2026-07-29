namespace DeckDesigner.Tests;

using DeckDesigner.Application.Optimization;

public class CutOptimizerTests
{
    private static readonly double[] Stock = { 3000, 3600, 4200, 4800, 5400 };

    [Fact]
    public void Optimize_PacksPiecesIntoStock()
    {
        var optimizer = new CutOptimizer();
        var plan = optimizer.Optimize(new double[] { 2000, 2000, 1000 }, Stock);

        Assert.True(plan.StockBoardCount >= 1);
        Assert.All(plan.StockBoards, b => Assert.True(b.UsedLength <= b.StockLength + 1e-6));
        Assert.Equal(5000, plan.TotalUsedLength, 3);
    }

    [Fact]
    public void Optimize_SingleBoardFitsSmallestStock()
    {
        var optimizer = new CutOptimizer();
        var plan = optimizer.Optimize(new double[] { 2900 }, Stock);

        var board = Assert.Single(plan.StockBoards);
        Assert.Equal(3000, board.StockLength, 3);
    }

    [Fact]
    public void Optimize_SplitsOversizedPieceIntoButtJointedSegments()
    {
        var optimizer = new CutOptimizer();

        // 12000mm run exceeds the largest 5400 stock: it should split into
        // 5400 + 5400 + 1200 segments, all fitting on stock boards.
        var plan = optimizer.Optimize(new double[] { 12000 }, Stock);

        Assert.All(plan.StockBoards, b => Assert.True(b.UsedLength <= b.StockLength + 1e-6));
        Assert.Equal(12000, plan.TotalUsedLength, 3);
        Assert.All(plan.StockBoards.SelectMany(b => b.Cuts), cut => Assert.True(cut <= 5400 + 1e-6));
    }
}

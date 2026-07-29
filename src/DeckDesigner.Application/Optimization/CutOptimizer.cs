namespace DeckDesigner.Application.Optimization;

/// <summary>
/// Optimizes required board lengths onto available stock lengths using
/// a Best Fit Decreasing bin-packing heuristic.
/// </summary>
public sealed class CutOptimizer
{
    public CutPlan Optimize(IEnumerable<double> requiredLengths, IReadOnlyList<double> stockLengths)
    {
        ArgumentNullException.ThrowIfNull(requiredLengths);
        ArgumentNullException.ThrowIfNull(stockLengths);

        if (stockLengths.Count == 0)
        {
            throw new ArgumentException("At least one stock length is required.", nameof(stockLengths));
        }

        var maxStock = stockLengths.Max();

        // Split any required length that is longer than the largest stock board into
        // multiple butt-joined segments so decks of any size can be built. Each segment
        // fits on a stock board and adjacent segments meet at a butt joint.
        var pieces = requiredLengths
            .Where(l => l > 0)
            .SelectMany(l => SplitIntoStockableSegments(l, maxStock))
            .OrderByDescending(l => l)
            .ToList();

        var open = new List<MutableStock>();

        foreach (var piece in pieces)
        {
            MutableStock? best = null;
            var bestRemaining = double.MaxValue;

            foreach (var stock in open)
            {
                var remaining = stock.Remaining;
                if (remaining + 1e-9 >= piece && remaining - piece < bestRemaining)
                {
                    best = stock;
                    bestRemaining = remaining - piece;
                }
            }

            if (best is null)
            {
                var chosen = SmallestStockThatFits(stockLengths, piece);
                best = new MutableStock(chosen);
                open.Add(best);
            }

            best.Cuts.Add(piece);
        }

        var result = open
            .Select(s => new StockBoard(s.StockLength, s.Cuts.ToArray()))
            .ToList();

        return new CutPlan(result);
    }

    /// <summary>
    /// Breaks a required length into segments that each fit on the largest stock board.
    /// A run longer than <paramref name="maxStock"/> becomes several full-length segments
    /// plus a remainder, joined end-to-end with butt joints.
    /// </summary>
    private static IEnumerable<double> SplitIntoStockableSegments(double length, double maxStock)
    {
        if (maxStock <= 0)
        {
            yield return length;
            yield break;
        }

        var remaining = length;
        while (remaining > maxStock + 1e-9)
        {
            yield return maxStock;
            remaining -= maxStock;
        }

        yield return remaining;
    }

    private static double SmallestStockThatFits(IReadOnlyList<double> stockLengths, double piece)
    {
        double? chosen = null;
        foreach (var length in stockLengths)
        {
            if (length + 1e-9 >= piece && (chosen is null || length < chosen))
            {
                chosen = length;
            }
        }

        return chosen ?? stockLengths.Max();
    }

    private sealed class MutableStock(double stockLength)
    {
        public double StockLength { get; } = stockLength;

        public List<double> Cuts { get; } = new();

        public double Remaining => StockLength - Cuts.Sum();
    }
}

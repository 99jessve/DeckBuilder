namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Places butt joints where decking boards must be joined end-to-end.
/// Follows common construction practice:
/// <list type="bullet">
/// <item>Every joint is centered over a joist so both board ends bear on framing.</item>
/// <item>Joint spacing along a row never exceeds the maximum stock board length.</item>
/// <item>Joints are staggered between adjacent rows so they never line up.</item>
/// </list>
/// </summary>
public sealed class ButtJointGenerator
{
    public IReadOnlyList<ButtJoint> Generate(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);

        var polygon = shape.Points;
        var bounds = shape.GetBounds();
        var pitch = settings.BoardWidth + settings.BoardGap;
        if (pitch <= 0 || settings.JoistSpacing <= 0 || settings.MaxBoardLength <= 0)
        {
            return Array.Empty<ButtJoint>();
        }

        var horizontal = settings.BoardDirection == BoardDirection.Horizontal;
        var joints = new List<ButtJoint>();

        var start = (horizontal ? bounds.MinY : bounds.MinX) + (settings.BoardWidth / 2.0);
        var limit = horizontal ? bounds.MaxY : bounds.MaxX;

        // Candidate joist lines along the board run axis.
        var joistStart = horizontal ? bounds.MinX : bounds.MinY;
        var joistLimit = horizontal ? bounds.MaxX : bounds.MaxY;

        var rowIndex = 0;
        for (var pos = start; pos <= limit; pos += pitch, rowIndex++)
        {
            var spans = horizontal
                ? ScanLine.HorizontalSpans(polygon, pos)
                : ScanLine.VerticalSpans(polygon, pos);

            foreach (var span in spans)
            {
                if (span.Length <= settings.MaxBoardLength)
                {
                    continue;
                }

                AddJointsForSpan(span, pos, rowIndex, horizontal, joistStart, joistLimit, settings, joints);
            }
        }

        return joints;
    }

    private static void AddJointsForSpan(
        ScanLine.Span span,
        double rowPos,
        int rowIndex,
        bool horizontal,
        double joistStart,
        double joistLimit,
        DeckSettings settings,
        List<ButtJoint> joints)
    {
        foreach (var joistRun in JointRuns(span, rowIndex, joistStart, joistLimit, settings))
        {
            joints.Add(CreateJoint(joistRun, rowPos, horizontal, settings.BoardWidth));
        }
    }

    /// <summary>
    /// Computes the run-axis positions where a span longer than the max board length is
    /// broken by butt joints. Positions are snapped to joists and staggered by row so
    /// this can be shared by both joint placement and field-board splitting.
    /// </summary>
    public static IReadOnlyList<double> JointRuns(
        ScanLine.Span span,
        int rowIndex,
        double joistStart,
        double joistLimit,
        DeckSettings settings)
    {
        var runs = new List<double>();
        if (span.Length <= settings.MaxBoardLength ||
            settings.MaxBoardLength <= 0 ||
            settings.JoistSpacing <= 0)
        {
            return runs;
        }

        // Stagger neighbouring rows by half the max board length so joints don't align.
        var stagger = (rowIndex % 2 == 0) ? 0.0 : settings.MaxBoardLength / 2.0;

        // Walk from the span start, adding a joint roughly every MaxBoardLength, snapped
        // to the nearest joist line so each joint bears on framing.
        var target = span.Start + settings.MaxBoardLength - stagger;

        // If the stagger pushed the first target past the span, fall back to one segment.
        if (target >= span.End)
        {
            target = span.Start + (settings.MaxBoardLength / 2.0);
        }

        var lastJointRun = double.NegativeInfinity;
        while (target < span.End - 1e-6)
        {
            var joistRun = SnapToJoist(target, joistStart, settings.JoistSpacing, joistLimit);

            // Keep the joint strictly inside the span and not on top of the previous one.
            if (joistRun > span.Start + 1e-6 &&
                joistRun < span.End - 1e-6 &&
                joistRun - lastJointRun > 1e-6)
            {
                runs.Add(joistRun);
                lastJointRun = joistRun;
            }

            target += settings.MaxBoardLength;
        }

        return runs;
    }

    private static double SnapToJoist(double run, double joistStart, double spacing, double joistLimit)
    {
        var index = Math.Round((run - joistStart) / spacing);
        var snapped = joistStart + (index * spacing);
        return Math.Clamp(snapped, joistStart, joistLimit);
    }

    private static ButtJoint CreateJoint(double run, double rowPos, bool horizontal, double boardWidth)
    {
        var half = boardWidth / 2.0;

        // The joint line is drawn across the board width, perpendicular to the run axis,
        // and carries the plank width so it renders as a real crossing board.
        return horizontal
            ? new ButtJoint(new Point2D(run, rowPos - half), new Point2D(run, rowPos + half), boardWidth)
            : new ButtJoint(new Point2D(rowPos - half, run), new Point2D(rowPos + half, run), boardWidth);
    }
}

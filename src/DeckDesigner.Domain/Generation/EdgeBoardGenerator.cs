namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Generates perimeter "picture-frame" boards so the deck has no exposed plank ends.
/// Field boards run in one direction, so the two edges parallel to that direction show
/// the clean side of a plank, while the remaining edges expose cut plank ends. This
/// generator lays a crossing board along every edge that would otherwise expose an end
/// (including a triangle's hypotenuse), inset inward by half a board width and mitered
/// at its ends so it keeps the drawn outline shape.
/// </summary>
public sealed class EdgeBoardGenerator
{
    /// <summary>
    /// A perimeter cover board together with the inner edge line that field boards
    /// must be trimmed against so they don't overhang the cover board. <see cref="EdgeStart"/>
    /// and <see cref="EdgeEnd"/> are the outline edge endpoints, used to limit the trim to
    /// boards that actually lie along this edge (important for concave, merged decks).
    /// </summary>
    public readonly record struct CoveredEdge(
        Board Board,
        GeometryHelpers.Line InnerLine,
        Point2D Inside,
        Point2D EdgeStart,
        Point2D EdgeEnd);

    public IReadOnlyList<Board> Generate(IShape shape, DeckSettings settings)
        => GenerateEdges(shape, settings).Select(e => e.Board).ToList();

    public IReadOnlyList<CoveredEdge> GenerateEdges(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);

        var points = shape.Points;
        if (points.Count < 3 || settings.BoardWidth <= 0)
        {
            return Array.Empty<CoveredEdge>();
        }

        // Orientation of the (possibly concave) outline: +1 if the vertices wind one way,
        // -1 the other. The inward normal is derived from this, which is reliable even
        // when the centroid falls outside a merged, L-shaped deck.
        var orient = SignedArea(points) >= 0 ? 1.0 : -1.0;
        var half = settings.BoardWidth / 2.0;
        var count = points.Count;

        // First pass: decide which edges need a cover board and compute each covered
        // edge's inner center-line (the outline edge offset inward by half a board).
        var covered = new bool[count];
        var innerLines = new GeometryHelpers.Line[count];

        for (var i = 0; i < count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % count];

            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var edgeLength = Math.Sqrt((ex * ex) + (ey * ey));
            if (edgeLength < 1e-6)
            {
                continue;
            }

            var ux = ex / edgeLength;
            var uy = ey / edgeLength;

            // Every edge gets a cover board so the deck has a full picture-frame border
            // with mitred corners on all sides, regardless of board run direction.

            // Inward normal from polygon winding (reliable even for concave, merged
            // outlines where the centroid can fall outside the shape).
            var nx = -uy * orient;
            var ny = ux * orient;

            covered[i] = true;

            // Inner edge of the cover board (full board width inward from the outline).
            var innerPoint = new Point2D(a.X + (nx * settings.BoardWidth), a.Y + (ny * settings.BoardWidth));
            innerLines[i] = new GeometryHelpers.Line(innerPoint, new Vector2D(ux, uy));
        }

        // Second pass: build the cover board center-lines, mitering each end against the
        // adjacent edge so corners meet cleanly and nothing overhangs the outline.
        var edges = new List<CoveredEdge>();
        for (var i = 0; i < count; i++)
        {
            if (!covered[i])
            {
                continue;
            }

            var a = points[i];
            var b = points[(i + 1) % count];

            var ex = b.X - a.X;
            var ey = b.Y - a.Y;
            var edgeLength = Math.Sqrt((ex * ex) + (ey * ey));
            var ux = ex / edgeLength;
            var uy = ey / edgeLength;

            var nx = -uy * orient;
            var ny = ux * orient;
            var midX = (a.X + b.X) / 2.0;
            var midY = (a.Y + b.Y) / 2.0;

            // Cover board center-line: outline edge offset inward by half a board width.
            var centerLine = new GeometryHelpers.Line(
                new Point2D(midX + (nx * half), midY + (ny * half)),
                new Vector2D(ux, uy));

            // The board's two long edges: the outline edge (outer) and the inner line.
            var outerEdge = new GeometryHelpers.Line(a, new Vector2D(ux, uy));
            var innerEdge = innerLines[i];

            var (startOuter, startInner) = MiterCorners(a, outerEdge, innerEdge, points, i, prev: true, covered, innerLines);
            var (endOuter, endInner) = MiterCorners(b, outerEdge, innerEdge, points, i, prev: false, covered, innerLines);

            // Spine endpoints (mid of each cut) keep Length and joint math meaningful.
            var start = Midpoint(startOuter, startInner);
            var end = Midpoint(endOuter, endInner);

            var corners = new[] { startOuter, endOuter, endInner, startInner };

            // Reference point strictly on the interior side of the inner line so field
            // board clipping keeps the interior (not the border) side regardless of shape.
            var inside = new Point2D(innerLines[i].Point.X + nx, innerLines[i].Point.Y + ny);

            edges.Add(new CoveredEdge(
                new Board(start, end, settings.BoardWidth, NeedsJoint: false, corners),
                innerLines[i],
                inside,
                a,
                b));
        }

        return edges;
    }

    private static Point2D Midpoint(Point2D p, Point2D q)
        => new((p.X + q.X) / 2.0, (p.Y + q.Y) / 2.0);

    // Computes the outer and inner corner points where this board's long edges are cut
    // against the neighbor edge. If the neighbor also carries a cover board, the cut is a
    // true mitre (outer meets neighbor's outer, inner meets neighbor's inner); otherwise
    // the board is cut flush along the neighbor's outline edge.
    private static (Point2D Outer, Point2D Inner) MiterCorners(
        Point2D corner,
        GeometryHelpers.Line outerEdge,
        GeometryHelpers.Line innerEdge,
        IReadOnlyList<Point2D> points,
        int edgeIndex,
        bool prev,
        bool[] covered,
        GeometryHelpers.Line[] innerLines)
    {
        var count = points.Count;
        var neighborIndex = prev ? (edgeIndex - 1 + count) % count : (edgeIndex + 1) % count;

        var na = points[neighborIndex];
        var nb = points[(neighborIndex + 1) % count];
        var ndx = nb.X - na.X;
        var ndy = nb.Y - na.Y;
        var nlen = Math.Sqrt((ndx * ndx) + (ndy * ndy));
        if (nlen < 1e-6)
        {
            return (corner, corner);
        }

        var ndir = new Vector2D(ndx / nlen, ndy / nlen);
        var neighborOuter = new GeometryHelpers.Line(na, ndir);
        var neighborInner = covered[neighborIndex] ? innerLines[neighborIndex] : neighborOuter;

        var outer = GeometryHelpers.Intersect(outerEdge, neighborOuter) ?? corner;
        var inner = GeometryHelpers.Intersect(innerEdge, neighborInner) ?? corner;
        return (outer, inner);
    }

    private static double SignedArea(IReadOnlyList<Point2D> points)
    {
        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2.0;
    }
}

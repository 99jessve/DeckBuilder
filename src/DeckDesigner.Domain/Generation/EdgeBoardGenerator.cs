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
    /// must be trimmed against so they don't overhang the cover board.
    /// </summary>
    public readonly record struct CoveredEdge(Board Board, GeometryHelpers.Line InnerLine, Point2D Inside);

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

        var horizontal = settings.BoardDirection == BoardDirection.Horizontal;

        // Unit run direction of the field boards.
        var runX = horizontal ? 1.0 : 0.0;
        var runY = horizontal ? 0.0 : 1.0;

        var centroid = Centroid(points);
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

            // If the edge is (nearly) parallel to the board run direction, it shows the
            // clean side of a plank and needs no cover board.
            var parallelism = Math.Abs((ux * runX) + (uy * runY));
            if (parallelism > 0.999)
            {
                continue;
            }

            // Inward normal: perpendicular to the edge, pointing toward the centroid.
            var nx = -uy;
            var ny = ux;
            var midX = (a.X + b.X) / 2.0;
            var midY = (a.Y + b.Y) / 2.0;
            if (((centroid.X - midX) * nx) + ((centroid.Y - midY) * ny) < 0)
            {
                nx = -nx;
                ny = -ny;
            }

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

            var nx = -uy;
            var ny = ux;
            var midX = (a.X + b.X) / 2.0;
            var midY = (a.Y + b.Y) / 2.0;
            if (((centroid.X - midX) * nx) + ((centroid.Y - midY) * ny) < 0)
            {
                nx = -nx;
                ny = -ny;
            }

            // Cover board center-line: outline edge offset inward by half a board width.
            var centerLine = new GeometryHelpers.Line(
                new Point2D(midX + (nx * half), midY + (ny * half)),
                new Vector2D(ux, uy));

            var start = MiterEnd(a, centerLine, points, i, prev: true, covered, centroid, half);
            var end = MiterEnd(b, centerLine, points, i, prev: false, covered, centroid, half);

            edges.Add(new CoveredEdge(
                new Board(start, end, settings.BoardWidth, NeedsJoint: false),
                innerLines[i],
                centroid));
        }

        return edges;
    }

    private static Point2D MiterEnd(
        Point2D corner,
        GeometryHelpers.Line centerLine,
        IReadOnlyList<Point2D> points,
        int edgeIndex,
        bool prev,
        bool[] covered,
        Point2D centroid,
        double half)
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
            return corner;
        }

        var ndir = new Vector2D(ndx / nlen, ndy / nlen);

        // Miter against the neighbor: if it also carries a cover board, meet at the
        // neighbor's center-line (offset inward by half); otherwise meet the outline edge.
        GeometryHelpers.Line neighborLine;
        if (covered[neighborIndex])
        {
            var noff = new Vector2D(-ndir.Y, ndir.X);
            var nmidX = (na.X + nb.X) / 2.0;
            var nmidY = (na.Y + nb.Y) / 2.0;
            if (((centroid.X - nmidX) * noff.X) + ((centroid.Y - nmidY) * noff.Y) < 0)
            {
                noff = -noff;
            }

            neighborLine = new GeometryHelpers.Line(
                new Point2D(nmidX + (noff.X * half), nmidY + (noff.Y * half)),
                ndir);
        }
        else
        {
            neighborLine = new GeometryHelpers.Line(na, ndir);
        }

        var hit = GeometryHelpers.Intersect(centerLine, neighborLine);
        
        // Apply proper mitre cut logic to ensure planks don't extend through the corner
        if (hit.HasValue)
        {
            return CreateProperMitreCut(hit.Value, corner, centerLine.Direction, half);
        }
        
        return hit ?? corner;
    }

    private static Point2D Centroid(IReadOnlyList<Point2D> points)
    {
        double sx = 0;
        double sy = 0;
        foreach (var p in points)
        {
            sx += p.X;
            sy += p.Y;
        }

        return new Point2D(sx / points.Count, sy / points.Count);
    }

    /// <summary>
    /// Creates a proper mitre cut that prevents planks from going through the mitre ends.
    /// </summary>
    private static Point2D CreateProperMitreCut(Point2D intersection, Point2D corner, Vector2D direction, double half)
    {
        // The key approach: create a proper cut perpendicular to board direction
        // This ensures planks don't go through the mitre end at the corner
        
        // Create perpendicular vector to board direction (for the cut)
        var cutDirection = new Vector2D(-direction.Y, direction.X);
        
        // Normalize it
        var cutMagnitude = Math.Sqrt((cutDirection.X * cutDirection.X) + (cutDirection.Y * cutDirection.Y));
        if (cutMagnitude > 1e-6)
        {
            cutDirection = new Vector2D(cutDirection.X / cutMagnitude, cutDirection.Y / cutMagnitude);
        }
        
        // Apply the offset to create a proper mitre cut
        return new Point2D(
            intersection.X + (cutDirection.X * half),
            intersection.Y + (cutDirection.Y * half));
    }
}

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

            // Clip the cover board to the deck outline. At a thin neck (e.g. where a
            // triangle corner just overlaps a rectangle side, leaving a sub-board-width
            // connecting segment) an unclipped mitred board overruns the outline into a
            // spike. Intersecting the convex board quad with the (possibly concave)
            // outline guarantees the border never pokes outside the shape.
            var clipped = ClipToOutline(corners, points);
            if (clipped.Count >= 3)
            {
                corners = clipped.ToArray();
            }

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

    // Intersects a convex board quad with the deck outline by clipping the outline polygon
    // against each quad edge's half-plane (Sutherland-Hodgman). Because the quad is convex,
    // this correctly handles a concave outline and keeps only the part of the board that
    // lies inside the shape.
    private static List<Point2D> ClipToOutline(
        IReadOnlyList<Point2D> quad,
        IReadOnlyList<Point2D> outline)
    {
        var cx = 0.0;
        var cy = 0.0;
        foreach (var p in quad)
        {
            cx += p.X;
            cy += p.Y;
        }

        var center = new Point2D(cx / quad.Count, cy / quad.Count);

        var result = new List<Point2D>(outline);
        for (var i = 0; i < quad.Count && result.Count >= 3; i++)
        {
            var a = quad[i];
            var b = quad[(i + 1) % quad.Count];
            result = GeometryHelpers.ClipPolygonToHalfPlane(result, GeometryHelpers.LineThrough(a, b), center);
        }

        return Dedupe(result);
    }

    // Removes consecutive duplicate vertices (within 1e-6) left behind by clipping so the
    // rendered polygon stays clean.
    private static List<Point2D> Dedupe(IReadOnlyList<Point2D> points)
    {
        var cleaned = new List<Point2D>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var q = points[(i + 1) % points.Count];
            if (Math.Abs(p.X - q.X) > 1e-6 || Math.Abs(p.Y - q.Y) > 1e-6)
            {
                cleaned.Add(p);
            }
        }

        return cleaned;
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

        // Miter limit: at a very acute junction (e.g. the thin neck where two snapped
        // shapes barely touch) the offset lines intersect far from the actual vertex,
        // producing a spike that pokes outside the outline. When the miter runs longer
        // than a few board widths, fall back to a flush cut so the border stays inside the
        // shape instead of shooting out into a spike.
        var maxMiter = MiterLimitFactor * BoardWidthOf(outerEdge, innerEdge);
        if (Distance(outer, corner) > maxMiter)
        {
            outer = corner;
        }

        if (Distance(inner, corner) > maxMiter)
        {
            // Flush inner cut: foot of the perpendicular from the vertex onto the inner line.
            inner = ClosestPointOnLine(innerEdge, corner);
        }

        // A mitre may only pull a corner *back* into this board, never push it *past* the
        // vertex along the edge. Against a steep neighbor (e.g. a hypotenuse at a merge
        // neck) the inner intersection overshoots beyond the edge endpoint, which slants
        // the board side inward into a visible taper/wedge. Clamp any such overshoot to a
        // flush cut so the board keeps a square end.
        // Outward along-edge direction at this corner: +edgeDir at the end vertex,
        // -edgeDir at the start vertex.
        var edgeDir = outerEdge.Direction;
        var sign = prev ? -1.0 : 1.0;
        if (((inner.X - corner.X) * edgeDir.X + (inner.Y - corner.Y) * edgeDir.Y) * sign > 1e-6)
        {
            inner = ClosestPointOnLine(innerEdge, corner);
        }

        if (((outer.X - corner.X) * edgeDir.X + (outer.Y - corner.Y) * edgeDir.Y) * sign > 1e-6)
        {
            outer = corner;
        }

        return (outer, inner);
    }

    private const double MiterLimitFactor = 3.0;

    private static double Distance(Point2D a, Point2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    // The board width equals the perpendicular distance between a board's outer and inner
    // long edges.
    private static double BoardWidthOf(GeometryHelpers.Line outerEdge, GeometryHelpers.Line innerEdge)
        => Math.Abs(GeometryHelpers.SignedDistance(outerEdge, innerEdge.Point));

    private static Point2D ClosestPointOnLine(GeometryHelpers.Line line, Point2D p)
    {
        var d = line.Direction;
        var t = ((p.X - line.Point.X) * d.X) + ((p.Y - line.Point.Y) * d.Y);
        return new Point2D(line.Point.X + (t * d.X), line.Point.Y + (t * d.Y));
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

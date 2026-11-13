namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using NetTopologySuite.Geometries;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

public sealed record DeckLayout(
    IReadOnlyList<Board> Boards,
    IReadOnlyList<Board> EdgeBoards,
    IReadOnlyList<Board> BreakerBoards,
    IReadOnlyList<DeckSection> Sections,
    IReadOnlyList<Joist> Joists,
    IReadOnlyList<BreakerDefinition> Dividers);

public sealed class DeckLayoutGenerator
{
    public DeckLayout Generate(Deck deck)
    {
        Validate(deck.Settings);
        var settings = deck.Settings;
        var outline = PolygonGeometry.ToPolygon(deck.Shape);
        var interior = PolygonGeometry.Offset(outline, -settings.BoardWidth);
        var stockLimit = Math.Min(settings.MaxBoardLength, settings.StockLengths.Max());
        var edges = new List<Board>();
        foreach (var board in new EdgeBoardGenerator().Generate(deck.Shape, settings))
        {
            if (board.Corners is not { Count: >= 3 }) continue;
            var footprint = PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(board.Corners));
            var clipped = (footprint.IsValid ? footprint : footprint.Buffer(0)).Intersection(outline);
            edges.AddRange(PhysicalPieces(clipped, Direction(board), stockLimit));
        }

        var dividers = deck.AutomaticDividers
            ? AutomaticDividers(interior, settings, stockLimit)
            : deck.Dividers.ToArray();
        if (dividers.Any(d => string.IsNullOrWhiteSpace(d.Id)) || dividers.Select(d => d.Id).Distinct().Count() != dividers.Count)
            throw new ArgumentException("Divider identities must be unique.");

        var sections = new List<(string Id, NtsGeometry Region)> { ("root", interior) };
        var breakers = new List<Board>();
        var reserved = PolygonGeometry.Factory.CreatePolygon();
        NtsGeometry occupied = reserved;
        var envelope = outline.EnvelopeInternal;
        var extent = Math.Max(envelope.Width, envelope.Height) + settings.BoardWidth + 1;
        foreach (var divider in dividers.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (!double.IsFinite(divider.Position) || !Enum.IsDefined(divider.Direction))
                throw new ArgumentException("Divider position and direction must be valid.");
            var vertical = divider.Direction == BoardDirection.Vertical;
            var lo = vertical ? envelope.MinX : envelope.MinY;
            var hi = vertical ? envelope.MaxX : envelope.MaxY;
            var half = settings.BoardWidth / 2;
            if (divider.Position <= lo + settings.BoardWidth + half || divider.Position >= hi - settings.BoardWidth - half)
                throw new InvalidOperationException("The divider must remain inside the picture-frame border.");
            var strip = vertical
                ? PolygonGeometry.Box(divider.Position - half, envelope.MinY - extent, divider.Position + half, envelope.MaxY + extent)
                : PolygonGeometry.Box(envelope.MinX - extent, divider.Position - half, envelope.MaxX + extent, divider.Position + half);
            var crossing = strip.Intersection(interior);
            if (crossing.IsEmpty) throw new InvalidOperationException("The divider does not cross the deck interior.");
            if (crossing.Intersection(occupied).Area > 1e-6)
                throw new InvalidOperationException("Breaker boards cannot overlap. Move or remove a divider first.");
            breakers.AddRange(PhysicalPieces(crossing, vertical ? new(0, 1) : new(1, 0), stockLimit));
            occupied = occupied.Union(crossing);
            var clearance = half + settings.BoardGap;
            var before = vertical
                ? PolygonGeometry.Box(envelope.MinX - extent, envelope.MinY - extent, divider.Position - clearance, envelope.MaxY + extent)
                : PolygonGeometry.Box(envelope.MinX - extent, envelope.MinY - extent, envelope.MaxX + extent, divider.Position - clearance);
            var after = vertical
                ? PolygonGeometry.Box(divider.Position + clearance, envelope.MinY - extent, envelope.MaxX + extent, envelope.MaxY + extent)
                : PolygonGeometry.Box(envelope.MinX - extent, divider.Position + clearance, envelope.MaxX + extent, envelope.MaxY + extent);
            var split = new List<(string, NtsGeometry)>();
            foreach (var section in sections)
            {
                var left = section.Region.Intersection(before);
                var right = section.Region.Intersection(after);
                if (!left.IsEmpty) split.Add(($"{section.Id}/{divider.Id}:before", left));
                if (!right.IsEmpty) split.Add(($"{section.Id}/{divider.Id}:after", right));
            }

            sections = split;
        }

        var result = new List<DeckSection>();
        var joists = new List<Joist>();
        foreach (var (id, region) in sections)
        {
            var parts = PolygonGeometry.Polygons(region).OrderBy(p => p.EnvelopeInternal.MinX).ThenBy(p => p.EnvelopeInternal.MinY).ToList();
            for (var i = 0; i < parts.Count; i++)
            {
                var sectionId = parts.Count == 1 ? id : $"{id}/part-{i}";
                var direction = deck.SectionDirections.GetValueOrDefault(sectionId, settings.BoardDirection);
                if (!Enum.IsDefined(direction)) throw new ArgumentException("The section direction must be valid.");
                var boards = FieldBoards(parts[i], direction, settings, envelope, stockLimit);
                var shape = PolygonGeometry.ToShape(parts[i]);
                result.Add(new(sectionId, shape, direction, boards));
                joists.AddRange(SectionJoists(parts[i], direction, settings));
            }
        }

        foreach (var divider in dividers)
        {
            var vertical = divider.Direction == BoardDirection.Vertical;
            var bearing = (settings.BoardWidth + settings.BoardGap) / 2;
            foreach (var position in new[] { divider.Position - bearing, divider.Position + bearing })
            {
                var line = PolygonGeometry.Factory.CreateLineString(vertical
                    ? new[] { new Coordinate(position, envelope.MinY), new Coordinate(position, envelope.MaxY) }
                    : new[] { new Coordinate(envelope.MinX, position), new Coordinate(envelope.MaxX, position) });
                AddSupports(joists, line.Intersection(outline), settings);
            }
        }

        // Stock-length breaks in border and breaker boards need bearing too.
        foreach (var board in edges.Concat(breakers))
        {
            var normal = Direction(board).Perpendicular();
            foreach (var end in new[] { board.Start, board.End })
            {
                var half = board.Width / 2;
                var line = PolygonGeometry.Factory.CreateLineString(new[]
                {
                    new Coordinate(end.X - normal.X * half, end.Y - normal.Y * half),
                    new Coordinate(end.X + normal.X * half, end.Y + normal.Y * half),
                });
                AddSupports(joists, line.Intersection(outline), settings);
            }
        }

        var all = result.SelectMany(s => s.Boards).Concat(edges).Concat(breakers).ToArray();
        return new(all, edges, breakers, result, joists, dividers);
    }

    private static void AddSupports(List<Joist> joists, NtsGeometry geometry, DeckSettings settings)
    {
        foreach (var line in Lines(geometry))
        {
            var a = line.GetCoordinateN(0);
            var b = line.GetCoordinateN(line.NumPoints - 1);
            var start = new Point2D(a.X, a.Y);
            var end = new Point2D(b.X, b.Y);
            var support = new Joist(start, end, settings.JoistWidth, settings.JoistHeight);
            var tolerance = Math.Max(0, (settings.JoistWidth - settings.BoardGap) / 2);
            if (joists.Any(j => Covers(j, support, tolerance))) continue;
            joists.RemoveAll(j => Covers(support, j, tolerance));
            joists.Add(support);
        }
    }

    private static bool Covers(Joist bearing, Joist other, double tolerance)
    {
        var u = new Vector2D(bearing.End.X - bearing.Start.X, bearing.End.Y - bearing.Start.Y).Normalized();
        var v = new Vector2D(other.End.X - other.Start.X, other.End.Y - other.Start.Y).Normalized();
        if (Math.Abs(u.X * v.Y - u.Y * v.X) > 1e-6) return false;
        return new[] { other.Start, other.End }.All(point =>
        {
            var delta = new Vector2D(point.X - bearing.Start.X, point.Y - bearing.Start.Y);
            var along = delta.Dot(u);
            return Math.Abs(delta.Dot(u.Perpendicular())) <= tolerance + 1e-6 &&
                along >= -1e-6 && along <= bearing.Length + 1e-6;
        });
    }

    private static IReadOnlyList<BreakerDefinition> AutomaticDividers(NtsGeometry interior, DeckSettings settings, double limit)
    {
        if (interior.IsEmpty) return Array.Empty<BreakerDefinition>();
        var bounds = interior.EnvelopeInternal;
        var horizontal = settings.BoardDirection == BoardDirection.Horizontal;
        var start = horizontal ? bounds.MinX : bounds.MinY;
        var length = horizontal ? bounds.Width : bounds.Height;
        var count = (int)Math.Ceiling(length / limit);
        return Enumerable.Range(1, Math.Max(0, count - 1)).Select(i => new BreakerDefinition(
            $"auto-{i}", horizontal ? BoardDirection.Vertical : BoardDirection.Horizontal, start + length * i / count)).ToArray();
    }

    private static IReadOnlyList<Board> FieldBoards(Polygon region, BoardDirection direction, DeckSettings settings, Envelope outline, double limit)
    {
        var result = new List<Board>();
        var horizontal = direction == BoardDirection.Horizontal;
        var bounds = region.EnvelopeInternal;
        var origin = horizontal ? outline.MinY : outline.MinX;
        var min = horizontal ? bounds.MinY : bounds.MinX;
        var max = horizontal ? bounds.MaxY : bounds.MaxX;
        var pitch = settings.BoardWidth + settings.BoardGap;
        var first = origin + Math.Floor((min - origin) / pitch) * pitch;
        for (var pos = first; pos < max - 1e-6; pos += pitch)
        {
            var strip = horizontal
                ? PolygonGeometry.Box(bounds.MinX - 1, pos, bounds.MaxX + 1, pos + settings.BoardWidth)
                : PolygonGeometry.Box(pos, bounds.MinY - 1, pos + settings.BoardWidth, bounds.MaxY + 1);
            foreach (var polygon in PolygonGeometry.Polygons(region.Intersection(strip)))
            {
                var board = ToBoard(polygon, horizontal ? new(1, 0) : new(0, 1));
                if (board.Length > limit + 1e-6)
                    throw new InvalidOperationException("This section needs another breaker board: a plank exceeds the available stock length.");
                if (board.Length > 1e-6 && board.Width > 1e-6) result.Add(board);
            }
        }

        return result;
    }

    public static IEnumerable<Board> PhysicalPieces(NtsGeometry geometry, Vector2D direction, double limit)
    {
        var u = direction.Normalized();
        var n = u.Perpendicular();
        foreach (var polygon in PolygonGeometry.Polygons(geometry))
        {
            var points = PolygonGeometry.Points(polygon.ExteriorRing);
            var lo = points.Min(p => p.X * u.X + p.Y * u.Y);
            var hi = points.Max(p => p.X * u.X + p.Y * u.Y);
            var minS = points.Min(p => p.X * n.X + p.Y * n.Y) - 1;
            var maxS = points.Max(p => p.X * n.X + p.Y * n.Y) + 1;
            var count = Math.Max(1, (int)Math.Ceiling((hi - lo) / limit));
            for (var i = 0; i < count; i++)
            {
                var a = lo + (hi - lo) * i / count;
                var b = lo + (hi - lo) * (i + 1) / count;
                Point2D At(double t, double s) => new(u.X * t + n.X * s, u.Y * t + n.Y * s);
                var slab = PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(new[]
                {
                    At(a, minS), At(b, minS), At(b, maxS), At(a, maxS),
                }));
                foreach (var part in PolygonGeometry.Polygons(polygon.Intersection(slab))) yield return ToBoard(part, u);
            }
        }
    }

    private static Board ToBoard(Polygon polygon, Vector2D u)
    {
        var n = u.Perpendicular();
        var corners = PolygonGeometry.Points(polygon.ExteriorRing);
        var lo = corners.Min(p => p.X * u.X + p.Y * u.Y);
        var hi = corners.Max(p => p.X * u.X + p.Y * u.Y);
        var minS = corners.Min(p => p.X * n.X + p.Y * n.Y);
        var maxS = corners.Max(p => p.X * n.X + p.Y * n.Y);
        var mid = (minS + maxS) / 2;
        return new(new(u.X * lo + n.X * mid, u.Y * lo + n.Y * mid),
            new(u.X * hi + n.X * mid, u.Y * hi + n.Y * mid), maxS - minS, false, corners);
    }

    private static IEnumerable<Joist> SectionJoists(Polygon polygon, BoardDirection direction, DeckSettings settings)
    {
        var bounds = polygon.EnvelopeInternal;
        var vertical = direction == BoardDirection.Horizontal;
        var start = vertical ? bounds.MinX : bounds.MinY;
        var end = vertical ? bounds.MaxX : bounds.MaxY;
        var count = Math.Max(1, (int)Math.Ceiling((end - start) / settings.JoistSpacing));
        for (var i = 0; i <= count; i++)
        {
            var pos = Math.Min(start + i * settings.JoistSpacing, end);
            var line = PolygonGeometry.Factory.CreateLineString(vertical
                ? new[] { new Coordinate(pos, bounds.MinY), new Coordinate(pos, bounds.MaxY) }
                : new[] { new Coordinate(bounds.MinX, pos), new Coordinate(bounds.MaxX, pos) });
            foreach (var segment in Lines(line.Intersection(polygon)))
            {
                var a = segment.GetCoordinateN(0);
                var b = segment.GetCoordinateN(segment.NumPoints - 1);
                yield return new(new(a.X, a.Y), new(b.X, b.Y), settings.JoistWidth, settings.JoistHeight);
            }
        }
    }

    public static IEnumerable<LineString> Lines(NtsGeometry geometry)
    {
        if (geometry is LineString line && line.Length > 1e-6) yield return line;
        else if (geometry is GeometryCollection collection)
            foreach (var child in collection.Geometries)
            foreach (var segment in Lines(child)) yield return segment;
    }

    private static Vector2D Direction(Board b) => new Vector2D(b.End.X - b.Start.X, b.End.Y - b.Start.Y).Normalized();

    public static void Validate(DeckSettings s)
    {
        var positive = new[] { s.BoardWidth, s.BoardThickness, s.MaxBoardLength, s.JoistSpacing, s.JoistWidth, s.JoistHeight };
        if (positive.Any(v => !double.IsFinite(v) || v <= 0) || !double.IsFinite(s.BoardGap) || s.BoardGap < 0 ||
            s.StockLengths.Count == 0 || s.StockLengths.Any(v => !double.IsFinite(v) || v <= 0) || !Enum.IsDefined(s.BoardDirection))
            throw new ArgumentException("Construction dimensions and stock lengths must be finite and positive; gaps cannot be negative.");
    }
}

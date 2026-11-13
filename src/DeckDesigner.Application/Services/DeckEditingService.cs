namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>Produces validated edits without modifying the source design.</summary>
public sealed class DeckEditingService
{
    public const double MinimumEdgeMm = 100;

    public static Deck Copy(Deck source) => new(source.Shape, source.Settings, source.Metadata)
    {
        Id = source.Id,
        Constraint = source.Constraint,
        AutomaticDividers = source.AutomaticDividers,
        Dividers = source.Dividers.ToList(),
        SectionDirections = new(source.SectionDirections),
    };

    public Deck ResizeCorner(Deck source, int corner, Point2D target)
    {
        var points = source.Shape.Points.ToArray();
        if (corner < 0 || corner >= points.Length) throw new ArgumentOutOfRangeException(nameof(corner));
        if (!double.IsFinite(target.X) || !double.IsFinite(target.Y))
            throw new ArgumentException("The corner must have finite coordinates.");

        if (source.Constraint == ShapeConstraint.Rectangle && points.Length == 4)
        {
            var opposite = (corner + 2) % 4;
            var next = (corner + 1) % 4;
            var previous = (corner + 3) % 4;
            var anchor = points[opposite];
            var u = Between(anchor, points[next]).Normalized();
            var v = Between(anchor, points[previous]).Normalized();
            var delta = Between(anchor, target);
            var width = delta.Dot(u);
            var height = delta.Dot(v);
            if (width < MinimumEdgeMm || height < MinimumEdgeMm)
                throw new InvalidOperationException("Keep rectangle sides at least 100 mm long; corners cannot cross.");
            points[next] = anchor.Translate(u * width);
            points[previous] = anchor.Translate(v * height);
            points[corner] = anchor.Translate(u * width + v * height);
        }
        else if (source.Constraint == ShapeConstraint.RightTriangle && points.Length == 3)
        {
            var u = Between(points[1], points[0]).Normalized();
            var v = Between(points[1], points[2]).Normalized();
            if (corner == 1)
            {
                var a = Between(target, points[0]).Dot(u);
                var b = Between(target, points[2]).Dot(v);
                if (a < MinimumEdgeMm || b < MinimumEdgeMm)
                    throw new InvalidOperationException("Keep both triangle legs at least 100 mm long.");
                points[1] = target;
                points[0] = target.Translate(u * a);
                points[2] = target.Translate(v * b);
            }
            else
            {
                var axis = corner == 0 ? u : v;
                var length = Between(points[1], target).Dot(axis);
                if (length < MinimumEdgeMm) throw new InvalidOperationException("Keep triangle legs at least 100 mm long.");
                points[corner] = points[1].Translate(axis * length);
            }
        }
        else
        {
            points[corner] = target;
        }

        if (points.Where((p, i) => p.DistanceTo(points[(i + 1) % points.Length]) < MinimumEdgeMm).Any())
            throw new InvalidOperationException("Keep outline edges at least 100 mm long.");
        var edited = Copy(source);
        edited.Shape = new PolygonShape(points, (source.Shape as PolygonShape)?.Holes);
        edited.RebuildGeometry();
        return edited;
    }

    public Deck Translate(Deck source, Vector2D offset)
    {
        var edited = Copy(source);
        edited.Shape = new PolygonShape(source.Shape.Points.Select(p => p.Translate(offset)),
            (source.Shape as PolygonShape)?.Holes.Select(h => (IReadOnlyList<Point2D>)h.Select(p => p.Translate(offset)).ToArray()));
        edited.Dividers = source.Dividers.Select(d => d with
        {
            Position = d.Position + (d.Direction == BoardDirection.Vertical ? offset.X : offset.Y),
        }).ToList();
        edited.RebuildGeometry();
        return edited;
    }

    public Deck MoveDivider(Deck source, string id, double position)
    {
        var edited = Copy(source);
        var dividers = source.EffectiveDividers.ToList();
        var index = dividers.FindIndex(d => d.Id == id);
        if (index < 0) throw new ArgumentException("Select an existing divider.", nameof(id));
        var current = dividers[index];
        if (dividers.Any(d => d.Id != id && d.Direction == current.Direction &&
            (d.Position - current.Position) * (d.Position - position) <= 0))
            throw new InvalidOperationException("Dividers cannot cross; keep the existing section order.");
        dividers[index] = dividers[index] with { Position = position };
        edited.AutomaticDividers = false;
        edited.Dividers = dividers;
        edited.RebuildGeometry();
        return edited;
    }

    public Deck SetSectionDirection(Deck source, string id, BoardDirection direction)
    {
        if (!source.Sections.Any(s => s.Id == id)) throw new ArgumentException("Select an existing section.", nameof(id));
        var edited = Copy(source);
        edited.AutomaticDividers = false;
        edited.Dividers = source.EffectiveDividers.ToList();
        edited.SectionDirections[id] = direction;
        edited.RebuildGeometry();
        return edited;
    }

    public int HitCorner(Deck source, Point2D point, double tolerance)
    {
        var nearest = -1;
        for (var i = 0; i < source.Shape.Points.Count; i++)
        {
            var distance = point.DistanceTo(source.Shape.Points[i]);
            if (distance > tolerance) continue;
            nearest = i;
            tolerance = distance;
        }
        return nearest;
    }

    public BreakerDefinition? HitDivider(Deck source, Point2D point, double tolerance)
    {
        var probe = PolygonGeometry.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(point.X, point.Y));
        if (!PolygonGeometry.ToPolygon(source.Shape).Covers(probe)) return null;
        return source.EffectiveDividers.OrderBy(d => Math.Abs(d.Position -
                (d.Direction == BoardDirection.Vertical ? point.X : point.Y)))
            .FirstOrDefault(d => Math.Abs(d.Position - (d.Direction == BoardDirection.Vertical ? point.X : point.Y))
                <= source.Settings.BoardWidth / 2 + tolerance && source.BreakerBoards.Any(b =>
                    b.Corners is { Count: >= 3 } && PolygonGeometry.Factory.CreatePolygon(PolygonGeometry.Ring(b.Corners))
                        .Distance(probe) <= tolerance));
    }

    public DeckSection? HitSection(Deck source, Point2D point)
    {
        var probe = PolygonGeometry.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(point.X, point.Y));
        return source.Sections.FirstOrDefault(s => PolygonGeometry.ToPolygon(s.Shape).Covers(probe));
    }

    private static Vector2D Between(Point2D a, Point2D b) => new(b.X - a.X, b.Y - a.Y);
}

namespace DeckDesigner.Domain.Geometry;

using DeckDesigner.Domain.Shapes;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

public static class PolygonGeometry
{
    public static GeometryFactory Factory { get; } = new();

    public static Polygon ToPolygon(IShape shape)
    {
        var shell = Ring(shape.Points);
        var holes = shape is PolygonShape p ? p.Holes.Select(Ring).ToArray() : Array.Empty<LinearRing>();
        var polygon = Factory.CreatePolygon(shell, holes);
        if (!polygon.IsValid || polygon.Area <= 1e-6)
        {
            throw new ArgumentException("The outline must be a valid, non-empty polygon.", nameof(shape));
        }

        return polygon;
    }

    public static LinearRing Ring(IReadOnlyList<Point2D> points)
    {
        if (points.Count < 3 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
        {
            throw new ArgumentException("An outline needs at least three finite points.", nameof(points));
        }

        return Factory.CreateLinearRing(points.Select(p => new Coordinate(p.X, p.Y))
            .Append(new Coordinate(points[0].X, points[0].Y)).ToArray());
    }

    public static PolygonShape ToShape(Polygon polygon) => new(
        Points(polygon.ExteriorRing),
        Enumerable.Range(0, polygon.NumInteriorRings).Select(i => Points(polygon.GetInteriorRingN(i))));

    public static IReadOnlyList<Point2D> Points(LineString ring) => ring.Coordinates.SkipLast(1)
        .Select(p => new Point2D(p.X, p.Y)).ToArray();

    public static IEnumerable<Polygon> Polygons(NtsGeometry geometry)
    {
        if (geometry is Polygon polygon && !polygon.IsEmpty && polygon.Area > 1e-6)
        {
            yield return polygon;
        }
        else if (geometry is GeometryCollection collection)
        {
            foreach (var child in collection.Geometries)
            foreach (var part in Polygons(child))
                yield return part;
        }
    }

    public static NtsGeometry Offset(NtsGeometry geometry, double distance) => geometry.Buffer(distance,
        new BufferParameters { JoinStyle = JoinStyle.Mitre, MitreLimit = 5 });

    public static Polygon Box(double minX, double minY, double maxX, double maxY) => Factory.CreatePolygon(new[]
    {
        new Coordinate(minX, minY), new Coordinate(maxX, minY), new Coordinate(maxX, maxY),
        new Coordinate(minX, maxY), new Coordinate(minX, minY),
    });
}

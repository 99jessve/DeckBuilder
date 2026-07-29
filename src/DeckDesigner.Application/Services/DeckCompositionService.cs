namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using NetTopologySuite.Geometries;

/// <summary>
/// Merges shapes that touch or overlap into unified deck outlines so that framing
/// and decking are generated continuously across snapped-together shapes.
/// </summary>
public sealed class DeckCompositionService
{
    // Shapes whose edges sit within this distance (mm) are treated as touching so they
    // merge the moment they snap together, even with a sub-millimeter gap.
    private const double MergeToleranceMm = 1.0;

    private readonly GeometryFactory _factory = new();

    /// <summary>
    /// Unions the given shapes and returns one <see cref="Deck"/> per connected region,
    /// with geometry already rebuilt on the merged outline.
    /// </summary>
    public IReadOnlyList<Deck> Compose(IReadOnlyList<IShape> shapes, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        ArgumentNullException.ThrowIfNull(settings);

        if (shapes.Count == 0)
        {
            return Array.Empty<Deck>();
        }

        var polygons = new List<Geometry>(shapes.Count);
        foreach (var shape in shapes)
        {
            var polygon = ToPolygon(shape);
            if (polygon is not null)
            {
                // Buffer(0) repairs any minor self-intersections/degeneracies.
                polygons.Add(polygon.IsValid ? polygon : polygon.Buffer(0));
            }
        }

        if (polygons.Count == 0)
        {
            return Array.Empty<Deck>();
        }

        // Grow each polygon by half the tolerance before unioning so shapes that are merely
        // touching (or a hair apart after snapping) fuse into one, then shrink the result
        // back so the outline dimensions are preserved. Mitre joins keep corners square.
        var half = MergeToleranceMm / 2.0;
        var bufferParams = new NetTopologySuite.Operation.Buffer.BufferParameters
        {
            JoinStyle = NetTopologySuite.Operation.Buffer.JoinStyle.Mitre,
            EndCapStyle = NetTopologySuite.Operation.Buffer.EndCapStyle.Square,
            MitreLimit = 5,
        };

        var buffered = polygons.Select(p => p.Buffer(half, bufferParams)).ToList();
        var union = _factory.BuildGeometry(buffered).Union().Buffer(-half, bufferParams);

        var decks = new List<Deck>();
        AppendPolygons(union, settings, decks);
        return decks;
    }

    private void AppendPolygons(Geometry geometry, DeckSettings settings, List<Deck> decks)
    {
        switch (geometry)
        {
            case Polygon polygon:
                var deck = BuildDeck(polygon, settings);
                if (deck is not null)
                {
                    decks.Add(deck);
                }

                break;

            case GeometryCollection collection:
                foreach (var g in collection.Geometries)
                {
                    AppendPolygons(g, settings, decks);
                }

                break;
        }
    }

    private static Deck? BuildDeck(Polygon polygon, DeckSettings settings)
    {
        var coordinates = polygon.ExteriorRing.Coordinates;
        if (coordinates.Length < 4)
        {
            return null;
        }

        // Drop the closing coordinate (equal to the first).
        var points = new List<Point2D>(coordinates.Length - 1);
        for (var i = 0; i < coordinates.Length - 1; i++)
        {
            points.Add(new Point2D(coordinates[i].X, coordinates[i].Y));
        }

        var deck = new Deck(new PolygonShape(points), settings);
        deck.RebuildGeometry();
        return deck;
    }

    private Polygon? ToPolygon(IShape shape)
    {
        var points = shape.Points;
        if (points.Count < 3)
        {
            return null;
        }

        var ring = new Coordinate[points.Count + 1];
        for (var i = 0; i < points.Count; i++)
        {
            ring[i] = new Coordinate(points[i].X, points[i].Y);
        }

        ring[points.Count] = new Coordinate(points[0].X, points[0].Y);

        return _factory.CreatePolygon(ring);
    }
}

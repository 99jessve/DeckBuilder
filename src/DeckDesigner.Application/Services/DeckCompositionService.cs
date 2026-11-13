namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using NetTopologySuite.Geometries;

public sealed record ComposedDeck(string Key, Deck Deck, IReadOnlyList<Guid> SourceIds);

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

    public IReadOnlyList<ComposedDeck> ComposeDesigns(IReadOnlyList<Deck> sources, DeckSettings settings,
        IReadOnlyDictionary<string, Deck> previous)
    {
        var result = new List<ComposedDeck>();
        foreach (var group in ConnectedGroups(sources, d => d.Shape))
        {
            var ids = group.Select(d => d.Id).OrderBy(id => id).ToArray();
            var key = string.Join("/", ids);
            if (group.Count == 1)
            {
                var single = DeckEditingService.Copy(group[0]);
                single.Settings = settings;
                single.RebuildGeometry();
                result.Add(new(key, single, ids));
                continue;
            }

            var outlines = Compose(group.Select(d => d.Shape).ToArray(), settings);
            for (var i = 0; i < outlines.Count; i++)
            {
                var partKey = outlines.Count == 1 ? key : $"{key}/part-{i}";
                var deck = outlines[i];
                if (previous.TryGetValue(partKey, out var authored))
                {
                    var edited = DeckEditingService.Copy(authored);
                    edited.Shape = deck.Shape;
                    edited.Settings = settings;
                    edited.RebuildGeometry();
                    deck = edited;
                }
                else if (group.Any(d => !d.AutomaticDividers || d.SectionDirections.Count > 0))
                {
                    throw new InvalidOperationException("Reset the customized layout to automatic before joining these shapes.");
                }
                result.Add(new(partKey, deck, ids));
            }
        }
        foreach (var (key, design) in previous)
        {
            if (design.AutomaticDividers && design.SectionDirections.Count == 0) continue;
            if (result.Any(c => c.Key == key)) continue;
            if (key.Split('/').Any(part => Guid.TryParse(part, out var id) && sources.Any(s => s.Id == id)))
                throw new InvalidOperationException("Reset the customized layout to automatic before joining or separating these shapes.");
        }
        return result;
    }

    private static List<List<T>> ConnectedGroups<T>(IReadOnlyList<T> sources, Func<T, IShape> shape)
    {
        var groups = new List<List<T>>();
        foreach (var source in sources)
        {
            var matches = groups.Where(g => g.Any(s => EdgeContact.Connected(shape(s), shape(source)))).ToList();
            var group = new List<T> { source };
            foreach (var match in matches)
            {
                group.AddRange(match);
                groups.Remove(match);
            }
            groups.Add(group);
        }
        return groups;
    }

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

        var groups = ConnectedGroups(shapes, s => s);

        var half = MergeToleranceMm / 2.0;
        var bufferParams = new NetTopologySuite.Operation.Buffer.BufferParameters
        {
            JoinStyle = NetTopologySuite.Operation.Buffer.JoinStyle.Mitre,
            EndCapStyle = NetTopologySuite.Operation.Buffer.EndCapStyle.Square,
            MitreLimit = 5,
        };

        var decks = new List<Deck>();
        foreach (var group in groups)
        {
            var polygons = group.Select(s => (Geometry)PolygonGeometry.ToPolygon(s)).ToList();
            var union = _factory.BuildGeometry(polygons).Union();
            if (group.Count > 1 && union is MultiPolygon)
            {
                union = _factory.BuildGeometry(polygons.Select(p => p.Buffer(half, bufferParams)).ToList())
                    .Union().Buffer(-half, bufferParams);
            }

            AppendPolygons(union, settings, decks);
        }
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

        var deck = new Deck(PolygonGeometry.ToShape(polygon), settings);
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

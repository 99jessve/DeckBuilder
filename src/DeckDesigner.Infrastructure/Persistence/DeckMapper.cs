namespace DeckDesigner.Infrastructure.Persistence;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Maps between domain decks and their serializable representations.
/// </summary>
public static class DeckMapper
{
    public static DeckDto ToDto(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        return new DeckDto
        {
            Id = deck.Id,
            Constraint = deck.Constraint,
            AutomaticDividers = deck.AutomaticDividers,
            Dividers = deck.Dividers.ToArray(),
            SectionDirections = new(deck.SectionDirections),
            ShapePoints = deck.Shape.Points.Select(p => new PointDto(p.X, p.Y)).ToArray(),
            Holes = deck.Shape is PolygonShape polygon
                ? polygon.Holes.Select(h => h.Select(p => new PointDto(p.X, p.Y)).ToArray()).ToArray()
                : Array.Empty<PointDto[]>(),
            Settings = ToDto(deck.Settings),
            Metadata = ToDto(deck.Metadata),
        };
    }

    public static Deck ToDomain(DeckDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var points = dto.ShapePoints.Select(p => new Point2D(p.X, p.Y));
        var shape = new PolygonShape(points, dto.Holes.Select(h => (IReadOnlyList<Point2D>)h.Select(p => new Point2D(p.X, p.Y)).ToArray()));
        var deck = new Deck(shape, ToDomain(dto.Settings), ToDomain(dto.Metadata))
        {
            Id = dto.Id,
            Constraint = dto.Constraint,
            AutomaticDividers = dto.AutomaticDividers,
            Dividers = dto.Dividers.ToList(),
            SectionDirections = new(dto.SectionDirections),
        };
        deck.RebuildGeometry();
        return deck;
    }

    private static DeckSettingsDto ToDto(DeckSettings settings) => new()
    {
        BoardWidth = settings.BoardWidth,
        BoardThickness = settings.BoardThickness,
        BoardGap = settings.BoardGap,
        BoardDirection = settings.BoardDirection.ToString(),
        MaxBoardLength = settings.MaxBoardLength,
        JoistSpacing = settings.JoistSpacing,
        JoistWidth = settings.JoistWidth,
        JoistHeight = settings.JoistHeight,
        ScrewsPerIntersection = settings.ScrewsPerIntersection,
        StockLengths = settings.StockLengths.ToArray(),
    };

    private static DeckSettings ToDomain(DeckSettingsDto dto) => new()
    {
        BoardWidth = dto.BoardWidth,
        BoardThickness = dto.BoardThickness,
        BoardGap = dto.BoardGap,
        BoardDirection = Enum.TryParse<BoardDirection>(dto.BoardDirection, out var dir)
            ? dir
            : BoardDirection.Horizontal,
        MaxBoardLength = dto.MaxBoardLength,
        JoistSpacing = dto.JoistSpacing,
        JoistWidth = dto.JoistWidth,
        JoistHeight = dto.JoistHeight,
        ScrewsPerIntersection = dto.ScrewsPerIntersection,
        StockLengths = dto.StockLengths.Length > 0 ? dto.StockLengths : new DeckSettings().StockLengths,
    };

    private static DeckMetadataDto ToDto(DeckMetadata metadata) => new()
    {
        Name = metadata.Name,
        Description = metadata.Description,
        CreatedUtc = metadata.CreatedUtc,
        ModifiedUtc = metadata.ModifiedUtc,
    };

    private static DeckMetadata ToDomain(DeckMetadataDto dto) => new()
    {
        Name = dto.Name,
        Description = dto.Description,
        CreatedUtc = dto.CreatedUtc,
        ModifiedUtc = dto.ModifiedUtc,
    };
}

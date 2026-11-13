namespace DeckDesigner.Infrastructure.Persistence;

using DeckDesigner.Domain.Model;

/// <summary>
/// Serializable snapshot of a deck outline point.
/// </summary>
public sealed record PointDto(double X, double Y);

/// <summary>
/// Serializable deck settings.
/// </summary>
public sealed record DeckSettingsDto
{
    public double BoardWidth { get; init; }
    public double BoardThickness { get; init; }
    public double BoardGap { get; init; }
    public string BoardDirection { get; init; } = "Horizontal";
    public double MaxBoardLength { get; init; }
    public double JoistSpacing { get; init; }
    public double JoistWidth { get; init; }
    public double JoistHeight { get; init; }
    public int ScrewsPerIntersection { get; init; }
    public double[] StockLengths { get; init; } = Array.Empty<double>();
}

/// <summary>
/// Serializable deck metadata.
/// </summary>
public sealed record DeckMetadataDto
{
    public string Name { get; init; } = "Untitled Deck";
    public string? Description { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset ModifiedUtc { get; init; }
}

/// <summary>
/// Serializable deck. Only the authored inputs are stored; geometry is regenerated on load.
/// </summary>
public sealed record DeckDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ShapeConstraint Constraint { get; init; }
    public bool AutomaticDividers { get; init; } = true;
    public BreakerDefinition[] Dividers { get; init; } = Array.Empty<BreakerDefinition>();
    public Dictionary<string, BoardDirection> SectionDirections { get; init; } = new();
    public PointDto[] ShapePoints { get; init; } = Array.Empty<PointDto>();
    public PointDto[][] Holes { get; init; } = Array.Empty<PointDto[]>();
    public DeckSettingsDto Settings { get; init; } = new();
    public DeckMetadataDto Metadata { get; init; } = new();
}

/// <summary>
/// Serializable project containing one or more decks.
/// </summary>
public sealed record DeckProjectDto
{
    public int Version { get; init; } = 2;
    public DeckDto[] Decks { get; init; } = Array.Empty<DeckDto>();
}

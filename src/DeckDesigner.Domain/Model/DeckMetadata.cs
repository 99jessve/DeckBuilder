namespace DeckDesigner.Domain.Model;

/// <summary>
/// Descriptive metadata for a deck.
/// </summary>
public sealed record DeckMetadata
{
    public string Name { get; init; } = "Untitled Deck";

    public string? Description { get; init; }

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedUtc { get; init; } = DateTimeOffset.UtcNow;
}

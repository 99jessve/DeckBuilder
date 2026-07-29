namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Model;

/// <summary>
/// Single coordination point for regenerating deck geometry.
/// </summary>
public sealed class GeometryService
{
    /// <summary>Raised after a deck's geometry has been rebuilt.</summary>
    public event EventHandler<Deck>? GeometryRebuilt;

    public void Rebuild(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        deck.RebuildGeometry();
        GeometryRebuilt?.Invoke(this, deck);
    }
}

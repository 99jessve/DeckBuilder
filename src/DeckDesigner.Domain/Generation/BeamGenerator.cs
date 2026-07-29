namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Creates supporting beams. Reserved for future structural rules.
/// </summary>
public sealed class BeamGenerator
{
    public IReadOnlyList<Beam> Generate(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);
        return Array.Empty<Beam>();
    }
}

namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Creates foundation posts. Reserved for future structural rules.
/// </summary>
public sealed class PostGenerator
{
    public IReadOnlyList<Post> Generate(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);
        return Array.Empty<Post>();
    }
}

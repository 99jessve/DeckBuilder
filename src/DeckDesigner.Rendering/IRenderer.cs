namespace DeckDesigner.Rendering;

using DeckDesigner.Domain.Model;

/// <summary>
/// A passive renderer for a specific kind of deck element. Renderers never generate geometry.
/// </summary>
public interface IRenderer<in T>
{
    void Render(IReadOnlyList<T> items, Camera camera, IRenderContext context);
}

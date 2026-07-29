namespace DeckDesigner.Rendering.Renderers;

using DeckDesigner.Domain.Model;

/// <summary>
/// Passive renderer that draws framing joists as lines. Generates no geometry.
/// </summary>
public sealed class JoistRenderer : IRenderer<Joist>
{
    private const string JoistColor = "#555555";

    public void Render(IReadOnlyList<Joist> items, Camera camera, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var joist in items)
        {
            var start = camera.WorldToScreen(joist.Start);
            var end = camera.WorldToScreen(joist.End);
            context.DrawLine(start.X, start.Y, end.X, end.Y, JoistColor, Math.Max(1, joist.Width * camera.Zoom));
        }
    }
}

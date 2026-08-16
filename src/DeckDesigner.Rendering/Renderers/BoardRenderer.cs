namespace DeckDesigner.Rendering.Renderers;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;

/// <summary>
/// Passive renderer that draws decking boards as lines. Generates no geometry.
/// </summary>
public sealed class BoardRenderer : IRenderer<Board>
{
    private const string BoardColor = "#8B5A2B";
    private const string OutlineColor = "#000000";

    public void Render(IReadOnlyList<Board> items, Camera camera, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var board in items)
        {
            var start = camera.WorldToScreen(board.Start);
            var end = camera.WorldToScreen(board.End);
            var thickness = Math.Max(1, board.Width * camera.Zoom);
            
            // Draw outline first (slightly thicker)
            context.DrawLine(start.X, start.Y, end.X, end.Y, OutlineColor, thickness + 2);
            
            // Draw the actual board
            context.DrawLine(start.X, start.Y, end.X, end.Y, BoardColor, thickness);
        }
    }
}

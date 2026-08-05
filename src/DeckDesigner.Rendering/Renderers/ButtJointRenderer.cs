namespace DeckDesigner.Rendering.Renderers;

using DeckDesigner.Domain.Model;

/// <summary>
/// Passive renderer that draws butt joints as a real crossing plank (board width and
/// board color) centered over a joist. Generates no geometry.
/// </summary>
public sealed class ButtJointRenderer : IRenderer<ButtJoint>
{
    private const string BoardColor = "#8B5A2B";

    public void Render(IReadOnlyList<ButtJoint> items, Camera camera, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var joint in items)
        {
            var start = camera.WorldToScreen(joint.Start);
            var end = camera.WorldToScreen(joint.End);
            context.DrawLine(start.X, start.Y, end.X, end.Y, BoardColor, Math.Max(1, joint.Width * camera.Zoom));
        }
    }
}

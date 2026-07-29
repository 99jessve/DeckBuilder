namespace DeckDesigner.Rendering.Renderers;

using DeckDesigner.Domain.Model;

/// <summary>
/// Passive renderer that draws butt joints as short lines across the board width,
/// centered over a joist. Generates no geometry.
/// </summary>
public sealed class ButtJointRenderer : IRenderer<ButtJoint>
{
    private const string JointColor = "#C0392B";

    public void Render(IReadOnlyList<ButtJoint> items, Camera camera, IRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var joint in items)
        {
            var start = camera.WorldToScreen(joint.Start);
            var end = camera.WorldToScreen(joint.End);
            context.DrawLine(start.X, start.Y, end.X, end.Y, JointColor, Math.Max(1.5, 2 * camera.Zoom));
        }
    }
}

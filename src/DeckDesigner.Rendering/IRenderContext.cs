namespace DeckDesigner.Rendering;

/// <summary>
/// Abstraction over a drawing surface so renderers stay independent of the UI toolkit.
/// Coordinates passed to draw calls are already in screen space.
/// </summary>
public interface IRenderContext
{
    void DrawLine(double x1, double y1, double x2, double y2, string colorHex, double thickness);

    void DrawPolygon(IReadOnlyList<(double X, double Y)> points, string strokeHex, double thickness, string? fillHex = null);

    void DrawCircle(double x, double y, double radius, string colorHex, double thickness);
}

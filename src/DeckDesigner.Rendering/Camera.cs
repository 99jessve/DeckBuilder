namespace DeckDesigner.Rendering;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// Maps between world coordinates (mm) and screen coordinates (pixels).
/// Supports pan, zoom and fit-to-view.
/// </summary>
public sealed class Camera
{
    public double Zoom { get; private set; } = 1.0;

    public Vector2D Pan { get; private set; } = Vector2D.Zero;

    public Point2D WorldToScreen(Point2D world) => new(
        (world.X * Zoom) + Pan.X,
        (world.Y * Zoom) + Pan.Y);

    public Point2D ScreenToWorld(Point2D screen) => new(
        (screen.X - Pan.X) / Zoom,
        (screen.Y - Pan.Y) / Zoom);

    public void SetZoom(double zoom)
    {
        if (zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be positive.");
        }

        Zoom = zoom;
    }

    public void PanBy(Vector2D delta) => Pan += delta;

    public void SetPan(Vector2D pan) => Pan = pan;

    /// <summary>Fits the given world bounds into a viewport of the given pixel size with margin.</summary>
    public void Fit(BoundingBox worldBounds, double viewportWidth, double viewportHeight, double marginPixels = 20)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var availableWidth = Math.Max(1, viewportWidth - (2 * marginPixels));
        var availableHeight = Math.Max(1, viewportHeight - (2 * marginPixels));

        var worldWidth = Math.Max(1e-6, worldBounds.Width);
        var worldHeight = Math.Max(1e-6, worldBounds.Height);

        Zoom = Math.Min(availableWidth / worldWidth, availableHeight / worldHeight);

        var center = worldBounds.Center;
        Pan = new Vector2D(
            (viewportWidth / 2.0) - (center.X * Zoom),
            (viewportHeight / 2.0) - (center.Y * Zoom));
    }
}

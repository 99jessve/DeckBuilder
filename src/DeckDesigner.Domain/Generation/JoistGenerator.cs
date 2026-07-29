namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Creates framing joists running perpendicular to the decking boards.
/// </summary>
public sealed class JoistGenerator
{
    public IReadOnlyList<Joist> Generate(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);

        var polygon = shape.Points;
        var bounds = shape.GetBounds();
        if (settings.JoistSpacing <= 0)
        {
            return Array.Empty<Joist>();
        }

        var joists = new List<Joist>();

        // Joists run perpendicular to the boards.
        var boardsHorizontal = settings.BoardDirection == BoardDirection.Horizontal;
        var joistsVertical = boardsHorizontal; // boards along X => joists along Y, stepping along X.

        var start = joistsVertical ? bounds.MinX : bounds.MinY;
        var limit = joistsVertical ? bounds.MaxX : bounds.MaxY;

        for (var pos = start; pos <= limit + 1e-6; pos += settings.JoistSpacing)
        {
            var spans = joistsVertical
                ? ScanLine.VerticalSpans(polygon, pos)
                : ScanLine.HorizontalSpans(polygon, pos);

            foreach (var span in spans)
            {
                var (startPoint, endPoint) = joistsVertical
                    ? (new Point2D(pos, span.Start), new Point2D(pos, span.End))
                    : (new Point2D(span.Start, pos), new Point2D(span.End, pos));

                joists.Add(new Joist(startPoint, endPoint, settings.JoistWidth, settings.JoistHeight));
            }
        }

        return joists;
    }
}

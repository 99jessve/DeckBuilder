namespace DeckDesigner.Domain.Generation;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

/// <summary>
/// Converts a deck outline into decking board segments.
/// </summary>
public sealed class BoardGenerator
{
    public IReadOnlyList<Board> Generate(IShape shape, DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(settings);

        var polygon = shape.Points;
        var bounds = shape.GetBounds();
        var pitch = settings.BoardWidth + settings.BoardGap;
        if (pitch <= 0)
        {
            return Array.Empty<Board>();
        }

        var boards = new List<Board>();
        var horizontal = settings.BoardDirection == BoardDirection.Horizontal;

        // Start half a board in so the first row sits inside the deck.
        var start = (horizontal ? bounds.MinY : bounds.MinX) + (settings.BoardWidth / 2.0);
        var limit = horizontal ? bounds.MaxY : bounds.MaxX;

        for (var pos = start; pos <= limit; pos += pitch)
        {
            var spans = horizontal
                ? ScanLine.HorizontalSpans(polygon, pos)
                : ScanLine.VerticalSpans(polygon, pos);

            foreach (var span in spans)
            {
                var needsJoint = span.Length > settings.MaxBoardLength;
                var (startPoint, endPoint) = horizontal
                    ? (new Point2D(span.Start, pos), new Point2D(span.End, pos))
                    : (new Point2D(pos, span.Start), new Point2D(pos, span.End));

                boards.Add(new Board(startPoint, endPoint, settings.BoardWidth, needsJoint));
            }
        }

        return boards;
    }
}

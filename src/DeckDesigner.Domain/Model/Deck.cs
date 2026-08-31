namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Generation;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Shapes;
/// <summary>
/// The central aggregate. Owns all generated construction geometry.
/// Geometry is produced exclusively through <see cref="RebuildGeometry"/>.
/// </summary>
public sealed class Deck
{
    private static readonly BoardGenerator BoardGen = new();
    private static readonly JoistGenerator JoistGen = new();
    private static readonly BeamGenerator BeamGen = new();
    private static readonly PostGenerator PostGen = new();
    private static readonly ButtJointGenerator ButtJointGen = new();
    private static readonly EdgeBoardGenerator EdgeBoardGen = new();

    private IReadOnlyList<Board> _boards = Array.Empty<Board>();
    private IReadOnlyList<Board> _edgeBoards = Array.Empty<Board>();
    private IReadOnlyList<Joist> _joists = Array.Empty<Joist>();
    private IReadOnlyList<Beam> _beams = Array.Empty<Beam>();
    private IReadOnlyList<Post> _posts = Array.Empty<Post>();
    private IReadOnlyList<ButtJoint> _buttJoints = Array.Empty<ButtJoint>();

    public Deck(IShape shape, DeckSettings? settings = null, DeckMetadata? metadata = null)
    {
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        Settings = settings ?? new DeckSettings();
        Metadata = metadata ?? new DeckMetadata();
    }

    public IShape Shape { get; set; }

    public DeckSettings Settings { get; set; }

    public DeckMetadata Metadata { get; set; }

    public IReadOnlyList<Board> Boards => _boards;

    /// <summary>Perimeter cover boards laid across edges that expose plank ends.</summary>
    public IReadOnlyList<Board> EdgeBoards => _edgeBoards;

    public IReadOnlyList<Joist> Joists => _joists;

    public IReadOnlyList<Beam> Beams => _beams;

    public IReadOnlyList<Post> Posts => _posts;

    public IReadOnlyList<ButtJoint> ButtJoints => _buttJoints;

    /// <summary>
    /// Regenerates all construction geometry from the current shape and settings.
    /// This is the single entry point for geometry generation.
    /// </summary>
    public void RebuildGeometry()
    {
        var field = BoardGen.Generate(Shape, Settings);
        var coveredEdges = EdgeBoardGen.GenerateEdges(Shape, Settings);
        _edgeBoards = coveredEdges.Select(e => e.Board).ToList();

        // Trim field boards back to each cover board's inner line so their square ends
        // don't poke past the (possibly angled) cover boards along the perimeter. The
        // full board rectangle is clipped so angled edges get a matching angled end, and
        // a partial row near a border survives as a narrower "cut" plank rather than
        // being dropped.
        var trimmed = new List<Board>(field.Count);
        foreach (var board in field)
        {
            // Build the board rectangle from its centerline and half width.
            var dir = new Vector2D(board.End.X - board.Start.X, board.End.Y - board.Start.Y).Normalized();
            var n = dir.Perpendicular();
            var h = board.Width / 2.0;
            var rect = new List<Point2D>
            {
                new(board.Start.X + (n.X * h), board.Start.Y + (n.Y * h)),
                new(board.End.X + (n.X * h), board.End.Y + (n.Y * h)),
                new(board.End.X - (n.X * h), board.End.Y - (n.Y * h)),
                new(board.Start.X - (n.X * h), board.Start.Y - (n.Y * h)),
            };

            foreach (var edge in coveredEdges)
            {
                rect = GeometryHelpers.ClipPolygonToHalfPlane(rect, edge.InnerLine, edge.Inside);
                if (rect.Count < 3)
                {
                    break;
                }
            }

            if (rect.Count < 3)
            {
                continue;
            }

            // Derive the plank's spine and (possibly reduced) width from the clipped
            // polygon, projecting its vertices onto the board's own axes.
            var minT = double.MaxValue;
            var maxT = double.MinValue;
            var minS = double.MaxValue;
            var maxS = double.MinValue;
            foreach (var p in rect)
            {
                var t = (p.X * dir.X) + (p.Y * dir.Y);
                var s = (p.X * n.X) + (p.Y * n.Y);
                minT = Math.Min(minT, t);
                maxT = Math.Max(maxT, t);
                minS = Math.Min(minS, s);
                maxS = Math.Max(maxS, s);
            }

            var width = maxS - minS;
            var sMid = (minS + maxS) / 2.0;
            if (maxT - minT <= 1e-6 || width <= 1e-6)
            {
                continue;
            }

            var start = new Point2D((dir.X * minT) + (n.X * sMid), (dir.Y * minT) + (n.Y * sMid));
            var end = new Point2D((dir.X * maxT) + (n.X * sMid), (dir.Y * maxT) + (n.Y * sMid));

            trimmed.Add(board with { Start = start, End = end, Width = width, Corners = rect });
        }

        // Boards exposes field boards plus perimeter cover boards for rendering/reporting.
        var all = new List<Board>(trimmed.Count + _edgeBoards.Count);
        all.AddRange(trimmed);
        all.AddRange(_edgeBoards);
        _boards = all;

        _joists = JoistGen.Generate(Shape, Settings);
        _beams = BeamGen.Generate(Shape, Settings);
        _posts = PostGen.Generate(Shape, Settings);
        _buttJoints = ButtJointGen.Generate(Shape, Settings);
    }
}

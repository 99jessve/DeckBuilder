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
    private static readonly DeckLayoutGenerator LayoutGen = new();
    private static readonly BeamGenerator BeamGen = new();
    private static readonly PostGenerator PostGen = new();

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
        Constraint = shape is RectangleShape ? ShapeConstraint.Rectangle : ShapeConstraint.FreePolygon;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public ShapeConstraint Constraint { get; set; }

    public bool AutomaticDividers { get; set; } = true;

    public List<BreakerDefinition> Dividers { get; set; } = new();

    public Dictionary<string, BoardDirection> SectionDirections { get; set; } = new();

    public IReadOnlyList<DeckSection> Sections { get; internal set; } = Array.Empty<DeckSection>();

    public IReadOnlyList<Board> BreakerBoards { get; internal set; } = Array.Empty<Board>();

    public IReadOnlyList<BreakerDefinition> EffectiveDividers { get; private set; } = Array.Empty<BreakerDefinition>();

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
        var layout = LayoutGen.Generate(this);
        var beams = BeamGen.Generate(Shape, Settings);
        var posts = PostGen.Generate(Shape, Settings);
        _boards = layout.Boards;
        _edgeBoards = layout.EdgeBoards;
        BreakerBoards = layout.BreakerBoards;
        Sections = layout.Sections;
        EffectiveDividers = layout.Dividers;
        _joists = layout.Joists;
        _beams = beams;
        _posts = posts;
        _buttJoints = Array.Empty<ButtJoint>();
    }
}

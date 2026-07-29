namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Generation;
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

    private IReadOnlyList<Board> _boards = Array.Empty<Board>();
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
        _boards = BoardGen.Generate(Shape, Settings);
        _joists = JoistGen.Generate(Shape, Settings);
        _beams = BeamGen.Generate(Shape, Settings);
        _posts = PostGen.Generate(Shape, Settings);
        _buttJoints = ButtJointGen.Generate(Shape, Settings);
    }
}

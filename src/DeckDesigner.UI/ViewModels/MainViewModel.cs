using System.Globalization;
using DeckDesigner.Application.Optimization;
using DeckDesigner.Application.Reporting;
using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

namespace DeckDesigner.UI.ViewModels;

/// <summary>
/// Coordinates a collection of deck shapes, tools, selection and the aggregated
/// material report for the UI.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    private readonly DeckEditingService _editingService = new();
    private readonly MaterialReportService _reportService = new(new CutOptimizer());
    private readonly DeckCompositionService _compositionService = new();
    private readonly List<ComposedDeck> _compositions = new();
    private Dictionary<string, Deck> _designs = new();
    private string? _selectedLayoutKey;
    private string? _selectedSectionId;

    private readonly List<Deck> _decks = new();

    // Decks produced by merging touching/overlapping shapes into single continuous
    // outlines. These drive framing/decking generation and the material report so
    // that snapped shapes are treated as one uniform deck.
    private readonly List<Deck> _mergedDecks = new();

    private DeckSettings _settings = new();
    private Deck? _selectedDeck;
    private DrawTool _currentTool = DrawTool.Rectangle;
    private string _statusText = "Rectangle tool: click and drag on the canvas to draw a deck.";
    private int _counter;

    // Aggregated report totals.
    private double _areaM2;
    private int _boardCount;
    private double _boardRunningM;
    private int _joistCount;
    private int _stockBoards;
    private double _wasteM;
    private int _screwCount;

    public MainViewModel()
    {
        ResetLayoutCommand = new RelayCommand(ResetSelectedLayout);
        Recompute();
    }

    /// <summary>Raised whenever geometry, selection or shapes change so the canvas can redraw.</summary>
    public event Action? DeckChanged;

    public IReadOnlyList<Deck> Decks => _decks;

    /// <summary>
    /// Continuous decks formed by unioning touching/overlapping shapes. Rendering of
    /// generated boards and joists should use these so snapped shapes look uniform.
    /// </summary>
    public IReadOnlyList<Deck> MergedDecks => _mergedDecks;

    public IReadOnlyList<ComposedDeck> Compositions => _compositions;

    public DeckSection? SelectedSection => _compositions.FirstOrDefault(c => c.Key == _selectedLayoutKey)?
        .Deck.Sections.FirstOrDefault(s => s.Id == _selectedSectionId);

    public bool HasSelectedSection => SelectedSection is not null;

    public bool SectionBoardsHorizontal
    {
        get => SelectedSection?.Direction == BoardDirection.Horizontal;
        set
        {
            if (_selectedLayoutKey is null || _selectedSectionId is null) return;
            EditLayout(_selectedLayoutKey, d => _editingService.SetSectionDirection(d, _selectedSectionId,
                value ? BoardDirection.Horizontal : BoardDirection.Vertical));
        }
    }

    public RelayCommand ResetLayoutCommand { get; }

    public Deck? SelectedDeck
    {
        get => _selectedDeck;
        private set
        {
            if (SetProperty(ref _selectedDeck, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => _selectedDeck is not null;

    public DrawTool CurrentTool
    {
        get => _currentTool;
        set
        {
            if (SetProperty(ref _currentTool, value))
            {
                OnPropertyChanged(nameof(IsSelectTool));
                OnPropertyChanged(nameof(IsRectangleTool));
                OnPropertyChanged(nameof(IsTriangleTool));
                StatusText = value switch
                {
                    DrawTool.Select => "Select: drag corners to resize, drag a breaker to move it, click a section to change its direction.",
                    DrawTool.Rectangle => "Rectangle tool: click and drag to draw a deck.",
                    DrawTool.Triangle => "Triangle tool: click three points to define a right-angled triangle.",
                    _ => StatusText,
                };
            }
        }
    }

    public bool IsSelectTool
    {
        get => _currentTool == DrawTool.Select;
        set { if (value) CurrentTool = DrawTool.Select; }
    }

    public bool IsRectangleTool
    {
        get => _currentTool == DrawTool.Rectangle;
        set { if (value) CurrentTool = DrawTool.Rectangle; }
    }

    public bool IsTriangleTool
    {
        get => _currentTool == DrawTool.Triangle;
        set { if (value) CurrentTool = DrawTool.Triangle; }
    }

    // Shared settings applied to every deck.
    public double BoardWidth
    {
        get => _settings.BoardWidth;
        set => UpdateSettings(_settings with { BoardWidth = ClampPositive(value, 20) });
    }

    public double BoardGap
    {
        get => _settings.BoardGap;
        set => UpdateSettings(_settings with { BoardGap = Math.Max(0, value) });
    }

    public double JoistSpacing
    {
        get => _settings.JoistSpacing;
        set => UpdateSettings(_settings with { JoistSpacing = ClampPositive(value, 50) });
    }

    public double MaxBoardLength
    {
        get => _settings.MaxBoardLength;
        set => UpdateSettings(_settings with { MaxBoardLength = ClampPositive(value, 500) });
    }

    public bool BoardsHorizontal
    {
        get => _settings.BoardDirection == BoardDirection.Horizontal;
        set => UpdateSettings(_settings with
        {
            BoardDirection = value ? BoardDirection.Horizontal : BoardDirection.Vertical,
        });
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string AreaText => $"{_areaM2.ToString("F2", Ci)} m²";
    public string BoardCountText => _boardCount.ToString(Ci);
    public string BoardRunningText => $"{_boardRunningM.ToString("F2", Ci)} m";
    public string JoistCountText => _joistCount.ToString(Ci);
    public string StockBoardText => _stockBoards.ToString(Ci);
    public string WasteText => $"{_wasteM.ToString("F2", Ci)} m";
    public string ScrewText => _screwCount.ToString(Ci);
    public string ShapeCountText => _decks.Count.ToString(Ci);

    /// <summary>Adds a new deck for the given shape without disturbing existing shapes.</summary>
    public Deck? AddShape(IShape shape, ShapeConstraint? constraint = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var deck = new Deck(shape, _settings, new DeckMetadata { Name = $"Deck {++_counter}" });
        if (constraint is not null) deck.Constraint = constraint.Value;
        if (!TryChange(() =>
        {
            deck.RebuildGeometry();
            CommitSources(_decks.Append(deck).ToList(), _settings);
        })) return null;
        SelectedDeck = deck;
        DeckChanged?.Invoke();
        var b = shape.GetBounds();
        var kind = shape.GetType().Name.Replace("Shape", string.Empty);
        StatusText = $"Added {kind} {b.Width.ToString("F0", Ci)} x {b.Height.ToString("F0", Ci)} mm";
        return deck;
    }

    /// <summary>Returns the topmost deck whose outline contains the world point, or null.</summary>
    public Deck? HitTest(Point2D world)
    {
        for (var i = _decks.Count - 1; i >= 0; i--)
        {
            if (PolygonGeometry.ToPolygon(_decks[i].Shape).Covers(
                PolygonGeometry.Factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(world.X, world.Y))))
            {
                return _decks[i];
            }
        }

        return null;
    }

    public void Select(Deck? deck)
    {
        SelectedDeck = deck;
        _selectedLayoutKey = null;
        _selectedSectionId = null;
        NotifySection();
        DeckChanged?.Invoke();
    }

    public void SelectSection(Point2D point)
    {
        _selectedLayoutKey = null;
        _selectedSectionId = null;
        foreach (var composition in _compositions.AsEnumerable().Reverse())
        {
            var section = _editingService.HitSection(composition.Deck, point);
            if (section is null) continue;
            _selectedLayoutKey = composition.Key;
            _selectedSectionId = section.Id;
            break;
        }
        NotifySection();
        DeckChanged?.Invoke();
    }

    public bool ResizeSelectedCorner(int corner, Point2D point) => EditSource(
        d => _editingService.ResizeCorner(d, corner, point));

    public bool MoveSelected(Deck baseline, Vector2D offset) => EditSource(
        _ => _editingService.Translate(baseline, offset));

    public bool MoveDivider(string layoutKey, string dividerId, double position) =>
        EditLayout(layoutKey, d => _editingService.MoveDivider(d, dividerId, position));

    private bool EditSource(Func<Deck, Deck> edit)
    {
        if (_selectedDeck is null) return false;
        return TryChange(() =>
        {
            var candidate = edit(_selectedDeck);
            CommitSources(_decks.Select(d => d.Id == candidate.Id ? candidate : d).ToList(), _settings);
            StatusText = "Design updated.";
        });
    }

    private bool EditLayout(string key, Func<Deck, Deck> edit)
    {
        var composition = _compositions.FirstOrDefault(c => c.Key == key);
        if (composition is null) return false;
        return TryChange(() =>
        {
            var candidate = edit(composition.Deck);
            var designs = new Dictionary<string, Deck>(_designs) { [key] = candidate };
            var sources = composition.SourceIds.Count == 1
                ? _decks.Select(d => d.Id == composition.SourceIds[0] ? candidate : d).ToList()
                : _decks.ToList();
            CommitSources(sources, _settings, designs);
            StatusText = "Layout updated. Drag its breaker boards or select another section.";
        });
    }

    private void ResetSelectedLayout()
    {
        var composition = _compositions.FirstOrDefault(c => c.Key == _selectedLayoutKey)
            ?? _compositions.FirstOrDefault(c => _selectedDeck is not null && c.SourceIds.Contains(_selectedDeck.Id));
        if (composition is null) return;
        EditLayout(composition.Key, d =>
        {
            var candidate = DeckEditingService.Copy(d);
            candidate.AutomaticDividers = true;
            candidate.Dividers.Clear();
            candidate.SectionDirections.Clear();
            candidate.RebuildGeometry();
            return candidate;
        });
    }

    public void DeleteSelected()
    {
        if (_selectedDeck is null)
        {
            return;
        }

        if (TryChange(() => CommitSources(_decks.Where(d => d.Id != _selectedDeck.Id).ToList(), _settings)))
            StatusText = "Shape deleted.";
    }

    private void UpdateSettings(DeckSettings settings)
    {
        TryChange(() => CommitSources(_decks.Select(d =>
        {
            var candidate = DeckEditingService.Copy(d);
            candidate.Settings = settings;
            return candidate;
        }).ToList(), settings));

        OnPropertyChanged(nameof(BoardWidth));
        OnPropertyChanged(nameof(BoardGap));
        OnPropertyChanged(nameof(JoistSpacing));
        OnPropertyChanged(nameof(MaxBoardLength));
        OnPropertyChanged(nameof(BoardsHorizontal));
    }

    private void Recompute() => CommitSources(_decks.ToList(), _settings);

    private void CommitSources(List<Deck> sources, DeckSettings settings, Dictionary<string, Deck>? designs = null)
    {
        var composed = _compositionService.ComposeDesigns(sources, settings, designs ?? _designs);
        var reports = composed.Select(c => _reportService.Generate(c.Deck)).ToArray();
        var selectedId = _selectedDeck?.Id;
        _decks.Clear();
        _decks.AddRange(sources);
        _settings = settings;
        SelectedDeck = _decks.FirstOrDefault(d => d.Id == selectedId);
        _compositions.Clear();
        _compositions.AddRange(composed);
        _designs = composed.ToDictionary(c => c.Key, c => c.Deck);
        _mergedDecks.Clear();
        _mergedDecks.AddRange(composed.Select(c => c.Deck));

        _areaM2 = 0;
        _boardCount = 0;
        _boardRunningM = 0;
        _joistCount = 0;
        _stockBoards = 0;
        _wasteM = 0;
        _screwCount = 0;

        foreach (var report in reports)
        {
            _areaM2 += report.AreaSquareMeters;
            _boardCount += report.BoardCount;
            _boardRunningM += report.BoardRunningMeters;
            _joistCount += report.JoistCount;
            _stockBoards += report.StockBoardCount;
            _wasteM += report.WasteMeters;
            _screwCount += report.ScrewCount;
        }

        OnPropertyChanged(nameof(AreaText));
        OnPropertyChanged(nameof(BoardCountText));
        OnPropertyChanged(nameof(BoardRunningText));
        OnPropertyChanged(nameof(JoistCountText));
        OnPropertyChanged(nameof(StockBoardText));
        OnPropertyChanged(nameof(WasteText));
        OnPropertyChanged(nameof(ScrewText));
        OnPropertyChanged(nameof(ShapeCountText));

        NotifySection();
        DeckChanged?.Invoke();
    }

    private bool TryChange(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NetTopologySuite.Geometries.TopologyException)
        {
            StatusText = ex.Message;
            NotifySection();
            return false;
        }
    }

    private void NotifySection()
    {
        OnPropertyChanged(nameof(SelectedSection));
        OnPropertyChanged(nameof(HasSelectedSection));
        OnPropertyChanged(nameof(SectionBoardsHorizontal));
    }

    private static double ClampPositive(double value, double min) => value < min ? min : value;
}

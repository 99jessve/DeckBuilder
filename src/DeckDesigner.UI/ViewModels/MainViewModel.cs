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

    private readonly GeometryService _geometryService = new();
    private readonly MaterialReportService _reportService = new(new CutOptimizer());
    private readonly DeckCompositionService _compositionService = new();

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
                    DrawTool.Select => "Select tool: click a shape to select, drag to move, Delete to remove.",
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
    public Deck AddShape(IShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var deck = new Deck(shape, _settings, new DeckMetadata { Name = $"Deck {++_counter}" });
        _geometryService.Rebuild(deck);
        _decks.Add(deck);
        SelectedDeck = deck;
        Recompute();
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
            if (PolygonMath.Contains(_decks[i].Shape.Points, world))
            {
                return _decks[i];
            }
        }

        return null;
    }

    public void Select(Deck? deck)
    {
        SelectedDeck = deck;
        DeckChanged?.Invoke();
    }

    /// <summary>Replaces the selected deck's shape (used while moving) and rebuilds it.</summary>
    public void ReplaceSelectedShape(IShape shape)
    {
        if (_selectedDeck is null)
        {
            return;
        }

        _selectedDeck.Shape = shape;
        _geometryService.Rebuild(_selectedDeck);
        Recompute();
    }

    public void DeleteSelected()
    {
        if (_selectedDeck is null)
        {
            return;
        }

        _decks.Remove(_selectedDeck);
        SelectedDeck = null;
        Recompute();
        StatusText = "Shape deleted.";
    }

    private void UpdateSettings(DeckSettings settings)
    {
        _settings = settings;
        foreach (var deck in _decks)
        {
            deck.Settings = settings;
            _geometryService.Rebuild(deck);
        }

        OnPropertyChanged(nameof(BoardWidth));
        OnPropertyChanged(nameof(BoardGap));
        OnPropertyChanged(nameof(JoistSpacing));
        OnPropertyChanged(nameof(MaxBoardLength));
        OnPropertyChanged(nameof(BoardsHorizontal));
        Recompute();
    }

    private void Recompute()
    {
        // Merge touching/overlapping shapes into continuous decks so framing and
        // decking run uniformly across snapped shapes, then report on those.
        _mergedDecks.Clear();
        _mergedDecks.AddRange(_compositionService.Compose(
            _decks.Select(d => d.Shape).ToList(),
            _settings));

        _areaM2 = 0;
        _boardCount = 0;
        _boardRunningM = 0;
        _joistCount = 0;
        _stockBoards = 0;
        _wasteM = 0;
        _screwCount = 0;

        foreach (var deck in _mergedDecks)
        {
            var report = _reportService.Generate(deck);
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

        DeckChanged?.Invoke();
    }

    private static double ClampPositive(double value, double min) => value < min ? min : value;
}

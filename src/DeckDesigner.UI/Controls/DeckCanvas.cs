using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DeckDesigner.Application.Services;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using DeckDesigner.Rendering;
using DeckDesigner.Rendering.Renderers;
using DeckDesigner.UI.Rendering;
using DeckDesigner.UI.ViewModels;

namespace DeckDesigner.UI.Controls;

/// <summary>
/// Interactive CAD-style canvas. Supports drawing multiple rectangle and triangle decks,
/// selecting and moving shapes with edge snapping, and deleting the selection.
/// </summary>
public sealed class DeckCanvas : Control
{
    private const double MinDrawSizeMm = 100;

    private readonly Camera _camera = new();
    private readonly BoardRenderer _boardRenderer = new();
    private readonly JoistRenderer _joistRenderer = new();
    private readonly ButtJointRenderer _buttJointRenderer = new();
    private readonly SnapService _snapService = new();

    private MainViewModel? _viewModel;
    private bool _fitted;

    // Drawing a new shape.
    private bool _isDrawing;
    private Point2D _drawStartWorld;
    private Point2D _drawCurrentWorld;

    // Three-click right-angled triangle creation.
    private readonly List<Point2D> _triangleClicks = new();
    private Point2D _hoverWorld;

    // Moving a selected shape.
    private bool _isMoving;
    private Point2D _moveStartWorld;
    private IReadOnlyList<Point2D> _moveBaseline = Array.Empty<Point2D>();

    // Panning the view.
    private bool _isPanning;
    private Point _lastPanScreen;

    public DeckCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.DeckChanged -= OnDeckChanged;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel is not null)
        {
            _viewModel.DeckChanged += OnDeckChanged;
        }

        _fitted = false;
        InvalidateVisual();
    }

    private void OnDeckChanged() => InvalidateVisual();

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (!_fitted && finalSize.Width > 0 && finalSize.Height > 0)
        {
            FitView(finalSize);
            _fitted = true;
        }

        return result;
    }

    private void FitView(Size size)
    {
        // Fit all shapes, or a sensible default region when the canvas is empty.
        var bounds = GetContentBounds();
        _camera.Fit(bounds, size.Width, size.Height, marginPixels: 40);
        InvalidateVisual();
    }

    private BoundingBox GetContentBounds()
    {
        if (_viewModel is null || _viewModel.Decks.Count == 0)
        {
            return new BoundingBox(0, 0, 6000, 4000);
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var deck in _viewModel.Decks)
        {
            var b = deck.Shape.GetBounds();
            minX = Math.Min(minX, b.MinX);
            minY = Math.Min(minY, b.MinY);
            maxX = Math.Max(maxX, b.MaxX);
            maxY = Math.Max(maxY, b.MaxY);
        }

        return new BoundingBox(minX, minY, maxX, maxY);
    }

    public void RequestFit()
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            FitView(Bounds.Size);
        }
    }

    private Point2D ToWorld(Point p) => _camera.ScreenToWorld(new Point2D(p.X, p.Y));

    // Shapes are placed on a 10mm grid, so coordinates land on 480, 490, 500, ...
    private const double GridStepMm = 10;

    private static Point2D SnapToGrid(Point2D world) => new(
        Math.Round(world.X / GridStepMm) * GridStepMm,
        Math.Round(world.Y / GridStepMm) * GridStepMm);

    private Point2D ToGridWorld(Point p) => SnapToGrid(ToWorld(p));

    // Constrains a point so the segment reference->point is axis-aligned (snaps to the
    // dominant axis). Used while drawing triangles with Ctrl held so the legs stay
    // straight along the grid.
    private static Point2D ConstrainToAxis(Point2D reference, Point2D point)
    {
        var dx = Math.Abs(point.X - reference.X);
        var dy = Math.Abs(point.Y - reference.Y);
        return dx >= dy
            ? new Point2D(point.X, reference.Y)
            : new Point2D(reference.X, point.Y);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        if (_viewModel is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var screen = point.Position;

        if (point.Properties.IsMiddleButtonPressed || point.Properties.IsRightButtonPressed)
        {
            _isPanning = true;
            _lastPanScreen = screen;
            e.Pointer.Capture(this);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var world = ToWorld(screen);

        if (_viewModel.CurrentTool == DrawTool.Select)
        {
            _triangleClicks.Clear();
            var hit = _viewModel.HitTest(world);
            _viewModel.Select(hit);

            if (hit is not null)
            {
                _isMoving = true;
                _moveStartWorld = world;
                _moveBaseline = hit.Shape.Points.ToArray();
                e.Pointer.Capture(this);
            }

            InvalidateVisual();
            return;
        }

        // Snap placement to the 10mm grid for the drawing/triangle tools.
        var gridWorld = SnapToGrid(world);

        // Triangle tool: three clicks define a right-angled triangle.
        if (_viewModel.CurrentTool == DrawTool.Triangle)
        {
            // Hold Ctrl to keep the leg straight along the grid relative to the first click.
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (ctrl && _triangleClicks.Count >= 1)
            {
                gridWorld = SnapToGrid(ConstrainToAxis(_triangleClicks[0], gridWorld));
            }

            _triangleClicks.Add(gridWorld);
            _hoverWorld = gridWorld;

            if (_triangleClicks.Count >= 3)
            {
                FinishTriangle();
            }
            else
            {
                _viewModel.StatusText = _triangleClicks.Count == 1
                    ? "Triangle: click the second point (the right-angle corner). Hold Ctrl for straight lines."
                    : "Triangle: click the third point to finish.";
            }

            InvalidateVisual();
            return;
        }

        // Drawing tools (rectangle drag).
        _triangleClicks.Clear();
        _isDrawing = true;
        _drawStartWorld = gridWorld;
        _drawCurrentWorld = gridWorld;
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_viewModel is null)
        {
            return;
        }

        var screen = e.GetPosition(this);

        if (_isPanning)
        {
            _camera.PanBy(new Vector2D(screen.X - _lastPanScreen.X, screen.Y - _lastPanScreen.Y));
            _lastPanScreen = screen;
            InvalidateVisual();
            return;
        }

        if (_isMoving)
        {
            MoveSelected(ToWorld(screen));
            return;
        }

        if (_isDrawing)
        {
            _drawCurrentWorld = ToGridWorld(screen);
            var w = Math.Abs(_drawCurrentWorld.X - _drawStartWorld.X);
            var h = Math.Abs(_drawCurrentWorld.Y - _drawStartWorld.Y);
            _viewModel.StatusText = $"Drawing {w:F0} x {h:F0} mm";
            InvalidateVisual();
        }
        else if (_viewModel.CurrentTool == DrawTool.Triangle && _triangleClicks.Count > 0)
        {
            // Live preview of the in-progress triangle following the cursor.
            _hoverWorld = ToGridWorld(screen);

            // Hold Ctrl to keep the second leg straight along the grid.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && _triangleClicks.Count == 1)
            {
                _hoverWorld = SnapToGrid(ConstrainToAxis(_triangleClicks[0], _hoverWorld));
            }

            if (_triangleClicks.Count == 1)
            {
                var length = _triangleClicks[0].DistanceTo(_hoverWorld);
                _viewModel.StatusText = $"Triangle: first side {length:F0} mm — click the right-angle corner.";
            }
            else if (_triangleClicks.Count == 2)
            {
                var length = SecondLegLength(_hoverWorld);
                _viewModel.StatusText = $"Triangle: second side {length:F0} mm — click to finish.";
            }

            InvalidateVisual();
        }
    }

    private void MoveSelected(Point2D world)
    {
        if (_viewModel is null || _moveBaseline.Count == 0)
        {
            return;
        }

        var offset = new Vector2D(world.X - _moveStartWorld.X, world.Y - _moveStartWorld.Y);

        // Quantize the drag to the 10mm grid so moved shapes stay on grid too.
        offset = new Vector2D(
            Math.Round(offset.X / GridStepMm) * GridStepMm,
            Math.Round(offset.Y / GridStepMm) * GridStepMm);

        var moved = _moveBaseline.Select(p => p.Translate(offset)).ToArray();
        var movedBounds = BoundingBox.FromPoints(moved);

        // Snap against the other shapes' bounds. Threshold ~ 12 px in world units.
        var others = _viewModel.Decks
            .Where(d => !ReferenceEquals(d, _viewModel.SelectedDeck))
            .Select(d => d.Shape.GetBounds());
        var threshold = 12.0 / Math.Max(_camera.Zoom, 1e-6);
        var snap = _snapService.ComputeSnap(movedBounds, others, threshold);

        var finalPoints = moved.Select(p => p.Translate(snap)).ToArray();
        _viewModel.ReplaceSelectedShape(new PolygonShape(finalPoints));
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            return;
        }

        if (_isMoving)
        {
            _isMoving = false;
            _moveBaseline = Array.Empty<Point2D>();
            e.Pointer.Capture(null);
            return;
        }

        if (_isDrawing && _viewModel is not null)
        {
            _isDrawing = false;
            e.Pointer.Capture(null);
            FinishDrawing();
            InvalidateVisual();
        }
    }

    private void FinishDrawing()
    {
        if (_viewModel is null)
        {
            return;
        }

        var minX = Math.Min(_drawStartWorld.X, _drawCurrentWorld.X);
        var minY = Math.Min(_drawStartWorld.Y, _drawCurrentWorld.Y);
        var maxX = Math.Max(_drawStartWorld.X, _drawCurrentWorld.X);
        var maxY = Math.Max(_drawStartWorld.Y, _drawCurrentWorld.Y);
        var width = maxX - minX;
        var height = maxY - minY;

        if (width < MinDrawSizeMm || height < MinDrawSizeMm)
        {
            return;
        }

        IShape shape = _viewModel.CurrentTool == DrawTool.Triangle
            ? new TriangleShape(
                new Point2D(minX, maxY),
                new Point2D(maxX, maxY),
                new Point2D((minX + maxX) / 2.0, minY))
            : new RectangleShape(new Point2D(minX, minY), width, height);

        _viewModel.AddShape(shape);
    }

    /// <summary>
    /// Builds a right-angled triangle from three clicks. The first click is one end of
    /// the first leg, the second click is the right-angle corner, and the third click
    /// determines the length/side of the perpendicular leg.
    /// </summary>
    private void FinishTriangle()
    {
        if (_viewModel is null || _triangleClicks.Count < 3)
        {
            return;
        }

        var a = _triangleClicks[0];
        var corner = _triangleClicks[1];
        var third = _triangleClicks[2];
        _triangleClicks.Clear();

        // First leg: corner -> a.
        var legX = a.X - corner.X;
        var legY = a.Y - corner.Y;
        var legLength = Math.Sqrt((legX * legX) + (legY * legY));

        if (legLength < MinDrawSizeMm)
        {
            _viewModel.StatusText = "Triangle too small; try again.";
            return;
        }

        // Perpendicular unit vector to the first leg.
        var perpX = -legY / legLength;
        var perpY = legX / legLength;

        // Project the third click onto the perpendicular to find the second leg length/side.
        var projection = ((third.X - corner.X) * perpX) + ((third.Y - corner.Y) * perpY);
        if (Math.Abs(projection) < MinDrawSizeMm)
        {
            _viewModel.StatusText = "Triangle too small; try again.";
            return;
        }

        var c = new Point2D(corner.X + (perpX * projection), corner.Y + (perpY * projection));
        _viewModel.AddShape(new TriangleShape(a, corner, c));
    }

    /// <summary>
    /// Length of the perpendicular second leg for the given cursor position, based on the
    /// two committed clicks. Used for the live status readout while drawing.
    /// </summary>
    private double SecondLegLength(Point2D cursor)
    {
        if (_triangleClicks.Count < 2)
        {
            return 0;
        }

        var a = _triangleClicks[0];
        var corner = _triangleClicks[1];
        var legX = a.X - corner.X;
        var legY = a.Y - corner.Y;
        var legLength = Math.Sqrt((legX * legX) + (legY * legY));
        if (legLength < 1e-6)
        {
            return 0;
        }

        var perpX = -legY / legLength;
        var perpY = legX / legLength;
        var projection = ((cursor.X - corner.X) * perpX) + ((cursor.Y - corner.Y) * perpY);
        return Math.Abs(projection);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var screen = e.GetPosition(this);
        var worldBefore = ToWorld(screen);

        var factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        _camera.SetZoom(Math.Clamp(_camera.Zoom * factor, 0.001, 100));

        var worldAfter = ToWorld(screen);
        _camera.PanBy(new Vector2D(
            (worldAfter.X - worldBefore.X) * _camera.Zoom,
            (worldAfter.Y - worldBefore.Y) * _camera.Zoom));

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_viewModel is null)
        {
            return;
        }

        if (e.Key is Key.Delete or Key.Back && _viewModel.HasSelection)
        {
            _viewModel.DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _triangleClicks.Count > 0)
        {
            _triangleClicks.Clear();
            _viewModel.StatusText = "Triangle cancelled.";
            InvalidateVisual();
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(new SolidColorBrush(Color.Parse("#1E1E1E")), new Rect(Bounds.Size));

        if (_viewModel is null)
        {
            return;
        }

        DrawGrid(context);

        var ctx = new AvaloniaRenderContext(context);

        // Draw the editable shape outlines/fills first so selection/move stays per-shape.
        foreach (var deck in _viewModel.Decks)
        {
            var selected = ReferenceEquals(deck, _viewModel.SelectedDeck);
            DrawOutline(context, deck.Shape, selected);
        }

        // Render generated boards/joists from the merged (unified) decks on top so that
        // snapped-together shapes are filled as one continuous deck.
        foreach (var deck in _viewModel.MergedDecks)
        {
            _joistRenderer.Render(deck.Joists, _camera, ctx);
            _boardRenderer.Render(deck.Boards, _camera, ctx);
            _buttJointRenderer.Render(deck.ButtJoints, _camera, ctx);
        }

        if (_isDrawing)
        {
            DrawPreview(context);
        }
        else if (_viewModel.CurrentTool == DrawTool.Triangle && _triangleClicks.Count > 0)
        {
            DrawTrianglePreview(context);
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        const double stepMm = 500;
        var pen = new Pen(new SolidColorBrush(Color.Parse("#2C2C2C")), 1);

        var topLeft = ToWorld(new Point(0, 0));
        var bottomRight = ToWorld(new Point(Bounds.Width, Bounds.Height));

        var startX = Math.Floor(topLeft.X / stepMm) * stepMm;
        var startY = Math.Floor(topLeft.Y / stepMm) * stepMm;

        if ((bottomRight.X - startX) / stepMm > 500 || (bottomRight.Y - startY) / stepMm > 500)
        {
            return;
        }

        for (var x = startX; x <= bottomRight.X; x += stepMm)
        {
            var a = _camera.WorldToScreen(new Point2D(x, topLeft.Y));
            var b = _camera.WorldToScreen(new Point2D(x, bottomRight.Y));
            context.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }

        for (var y = startY; y <= bottomRight.Y; y += stepMm)
        {
            var a = _camera.WorldToScreen(new Point2D(topLeft.X, y));
            var b = _camera.WorldToScreen(new Point2D(bottomRight.X, y));
            context.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
        }
    }

    private void DrawOutline(DrawingContext context, IShape shape, bool selected)
    {
        var points = shape.Points;
        if (points.Count < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            var first = _camera.WorldToScreen(points[0]);
            g.BeginFigure(new Point(first.X, first.Y), true);
            for (var i = 1; i < points.Count; i++)
            {
                var p = _camera.WorldToScreen(points[i]);
                g.LineTo(new Point(p.X, p.Y));
            }

            g.EndFigure(true);
        }

        var fill = new SolidColorBrush(Color.Parse("#2A2118"));
        var strokeColor = selected ? "#4EC9B0" : "#E0A868";
        var pen = new Pen(new SolidColorBrush(Color.Parse(strokeColor)), selected ? 3 : 2) { LineJoin = PenLineJoin.Round };
        context.DrawGeometry(fill, pen, geometry);
    }

    private void DrawPreview(DrawingContext context)
    {
        var minX = Math.Min(_drawStartWorld.X, _drawCurrentWorld.X);
        var minY = Math.Min(_drawStartWorld.Y, _drawCurrentWorld.Y);
        var maxX = Math.Max(_drawStartWorld.X, _drawCurrentWorld.X);
        var maxY = Math.Max(_drawStartWorld.Y, _drawCurrentWorld.Y);
        var pen = new Pen(new SolidColorBrush(Color.Parse("#4EC9B0")), 1.5, DashStyle.Dash);
        var fill = new SolidColorBrush(Color.Parse("#204EC9B0"));

        var p1 = _camera.WorldToScreen(new Point2D(minX, minY));
        var p2 = _camera.WorldToScreen(new Point2D(maxX, maxY));
        context.DrawRectangle(fill, pen, new Rect(new Point(p1.X, p1.Y), new Point(p2.X, p2.Y)));
    }

    private void DrawTrianglePreview(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color.Parse("#4EC9B0")), 1.5, DashStyle.Dash) { LineJoin = PenLineJoin.Round };
        var fill = new SolidColorBrush(Color.Parse("#204EC9B0"));
        var dotBrush = new SolidColorBrush(Color.Parse("#4EC9B0"));

        // Build the point list: committed clicks plus a projected preview for the cursor.
        var points = new List<Point2D>(_triangleClicks);

        if (_triangleClicks.Count == 1)
        {
            points.Add(_hoverWorld);
        }
        else if (_triangleClicks.Count == 2)
        {
            var a = _triangleClicks[0];
            var corner = _triangleClicks[1];
            var legX = a.X - corner.X;
            var legY = a.Y - corner.Y;
            var legLength = Math.Sqrt((legX * legX) + (legY * legY));
            if (legLength > 1e-6)
            {
                var perpX = -legY / legLength;
                var perpY = legX / legLength;
                var projection = ((_hoverWorld.X - corner.X) * perpX) + ((_hoverWorld.Y - corner.Y) * perpY);
                points = new List<Point2D>
                {
                    a,
                    corner,
                    new(corner.X + (perpX * projection), corner.Y + (perpY * projection)),
                };
            }
        }

        if (points.Count >= 2)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                var first = _camera.WorldToScreen(points[0]);
                g.BeginFigure(new Point(first.X, first.Y), points.Count >= 3);
                for (var i = 1; i < points.Count; i++)
                {
                    var p = _camera.WorldToScreen(points[i]);
                    g.LineTo(new Point(p.X, p.Y));
                }

                g.EndFigure(points.Count >= 3);
            }

            context.DrawGeometry(fill, pen, geometry);
        }

        // Draw the committed click markers.
        foreach (var click in _triangleClicks)
        {
            var s = _camera.WorldToScreen(click);
            context.DrawEllipse(dotBrush, null, new Point(s.X, s.Y), 4, 4);
        }
    }
}

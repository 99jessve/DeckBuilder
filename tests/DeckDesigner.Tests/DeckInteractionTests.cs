namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;
using DeckDesigner.UI.ViewModels;

/// <summary>Window-free integration tests for the same commands used by the canvas.</summary>
public class DeckInteractionTests
{
    private static MainViewModel SingleDeck()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.AddShape(new RectangleShape(Point2D.Origin, 9000, 3000)));
        vm.CurrentTool = DrawTool.Select;
        return vm;
    }

    [Fact]
    public void CornerDrag_RebuildsVisibleGeometryAndReport()
    {
        var vm = SingleDeck();
        var originalArea = vm.AreaText;
        Assert.True(vm.ResizeSelectedCorner(2, new(10000, 3500)));
        Assert.Equal(10000, vm.SelectedDeck!.Shape.GetBounds().Width);
        Assert.Equal(35000000, Assert.Single(vm.MergedDecks).Shape.Area);
        Assert.NotEqual(originalArea, vm.AreaText);
    }

    [Fact]
    public void DividerDragAndSectionDirection_PersistThroughSettingsChange()
    {
        var vm = SingleDeck();
        var key = Assert.Single(vm.Compositions).Key;
        Assert.True(vm.MoveDivider(key, "auto-1", 4200));
        vm.SelectSection(new(1000, 1000));
        Assert.True(vm.HasSelectedSection);
        vm.SectionBoardsHorizontal = false;
        vm.JoistSpacing = 350;
        var deck = Assert.Single(vm.MergedDecks);
        Assert.Equal(4200, Assert.Single(deck.EffectiveDividers).Position);
        Assert.Equal(BoardDirection.Vertical, vm.SelectedSection!.Direction);
        Assert.Equal(2, deck.Sections.Count);
        vm.ResetLayoutCommand.Execute(null);
        Assert.Equal(4500, Assert.Single(Assert.Single(vm.MergedDecks).EffectiveDividers).Position);
        Assert.All(vm.MergedDecks[0].Sections, s => Assert.Equal(BoardDirection.Horizontal, s.Direction));
    }

    [Fact]
    public void InvalidDragAndSettings_RetainLastValidDesignAndReport()
    {
        var vm = SingleDeck();
        var deck = Assert.Single(vm.MergedDecks);
        var report = vm.BoardCountText;
        var key = Assert.Single(vm.Compositions).Key;
        Assert.False(vm.MoveDivider(key, "auto-1", 8000));
        Assert.Same(deck, Assert.Single(vm.MergedDecks));
        Assert.Equal(report, vm.BoardCountText);
        vm.JoistSpacing = double.NaN;
        Assert.Equal(400, vm.JoistSpacing);
        Assert.Same(deck, Assert.Single(vm.MergedDecks));
    }

    [Fact]
    public void MergedLayout_RetainsEditsAcrossSourceResize()
    {
        var vm = new MainViewModel();
        vm.AddShape(new RectangleShape(Point2D.Origin, 4000, 3000));
        vm.AddShape(new RectangleShape(new(4000, 0), 4000, 3000));
        var key = Assert.Single(vm.Compositions).Key;
        Assert.True(vm.MoveDivider(key, "auto-1", 3800));
        vm.SelectSection(new(1000, 1000));
        vm.SectionBoardsHorizontal = false;
        Assert.True(vm.ResizeSelectedCorner(2, new(8500, 3200)));
        var merged = Assert.Single(vm.MergedDecks);
        Assert.Equal(3800, Assert.Single(merged.EffectiveDividers).Position);
        Assert.Contains(merged.Sections, s => s.Direction == BoardDirection.Vertical);
    }

    [Fact]
    public void CustomizedGroup_CannotSilentlyLoseEditsWhenSeparated()
    {
        var vm = new MainViewModel();
        vm.AddShape(new RectangleShape(Point2D.Origin, 4000, 3000));
        vm.AddShape(new RectangleShape(new(4000, 0), 4000, 3000));
        var key = Assert.Single(vm.Compositions).Key;
        Assert.True(vm.MoveDivider(key, "auto-1", 3800));
        var source = vm.SelectedDeck!;
        Assert.False(vm.MoveSelected(source, new(1000, 0)));
        Assert.Single(vm.MergedDecks);
        Assert.Contains("Reset", vm.StatusText);
        vm.ResetLayoutCommand.Execute(null);
        Assert.True(vm.MoveSelected(vm.SelectedDeck!, new(1000, 0)));
        Assert.Equal(2, vm.MergedDecks.Count);
    }

    [Fact]
    public void Movement_UsesOriginalBaselineAndPreservesConstraints()
    {
        var vm = SingleDeck();
        var key = Assert.Single(vm.Compositions).Key;
        Assert.True(vm.MoveDivider(key, "auto-1", 4200));
        var baseline = vm.SelectedDeck!;
        Assert.True(vm.MoveSelected(baseline, new(100, 0)));
        Assert.True(vm.MoveSelected(baseline, new(200, 0)));
        Assert.Equal(200, vm.SelectedDeck!.Shape.GetBounds().MinX);
        Assert.Equal(4400, Assert.Single(vm.MergedDecks[0].EffectiveDividers).Position);
        Assert.Equal(ShapeConstraint.Rectangle, vm.SelectedDeck.Constraint);
    }
}

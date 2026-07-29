namespace DeckDesigner.Tests;

using DeckDesigner.Domain.Generation;
using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Model;
using DeckDesigner.Domain.Shapes;

public class ButtJointGeneratorTests
{
    [Fact]
    public void Generate_ShortDeck_ProducesNoJoints()
    {
        var gen = new ButtJointGenerator();
        var settings = new DeckSettings { MaxBoardLength = 5400 };
        var shape = new RectangleShape(new Point2D(0, 0), 3000, 2000);

        var joints = gen.Generate(shape, settings);

        Assert.Empty(joints);
    }

    [Fact]
    public void Generate_LongDeck_PlacesJointsOnJoists()
    {
        var gen = new ButtJointGenerator();
        var settings = new DeckSettings
        {
            MaxBoardLength = 3000,
            JoistSpacing = 400,
            BoardDirection = BoardDirection.Horizontal,
        };

        // 9000mm run needs at least two joints per row (9000 / 3000).
        var shape = new RectangleShape(new Point2D(0, 0), 9000, 2000);

        var joints = gen.Generate(shape, settings);

        Assert.NotEmpty(joints);

        // Each joint must sit on a joist line (multiple of the joist spacing).
        foreach (var joint in joints)
        {
            var offset = joint.Center.X % settings.JoistSpacing;
            var onJoist = offset < 1e-6 || Math.Abs(offset - settings.JoistSpacing) < 1e-6;
            Assert.True(onJoist, $"Joint at X={joint.Center.X} is not centered on a joist.");
        }
    }

    [Fact]
    public void Generate_AdjacentRows_StaggerJoints()
    {
        var gen = new ButtJointGenerator();
        var settings = new DeckSettings
        {
            MaxBoardLength = 3000,
            JoistSpacing = 400,
            BoardWidth = 145,
            BoardGap = 5,
            BoardDirection = BoardDirection.Horizontal,
        };
        var shape = new RectangleShape(new Point2D(0, 0), 9000, 2000);

        var joints = gen.Generate(shape, settings);

        // Group joints by row (Y) and ensure not every row shares the same joint X set.
        var rows = joints.GroupBy(j => Math.Round(j.Center.Y, 1)).ToList();
        Assert.True(rows.Count >= 2);

        var distinctXSets = rows
            .Select(r => string.Join(",", r.Select(j => Math.Round(j.Center.X)).OrderBy(x => x)))
            .Distinct()
            .Count();

        Assert.True(distinctXSets > 1, "Joints should be staggered between adjacent rows.");
    }
}

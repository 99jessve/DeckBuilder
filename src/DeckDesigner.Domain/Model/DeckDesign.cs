namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Shapes;

public enum ShapeConstraint
{
    FreePolygon,
    Rectangle,
    RightTriangle,
}

public sealed record BreakerDefinition(string Id, BoardDirection Direction, double Position);

public sealed record DeckSection(string Id, IShape Shape, BoardDirection Direction, IReadOnlyList<Board> Boards);

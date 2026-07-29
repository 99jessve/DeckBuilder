namespace DeckDesigner.Domain.Model;

using DeckDesigner.Domain.Geometry;

/// <summary>
/// A foundation post (mm). Reserved for future structural generation.
/// </summary>
public sealed record Post(Point2D Location, double Size);

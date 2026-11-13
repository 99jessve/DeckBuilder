namespace DeckDesigner.Application.Services;

using DeckDesigner.Domain.Geometry;
using DeckDesigner.Domain.Shapes;

public static class EdgeContact
{
    public const double MinimumContactMm = 100;

    public static IEnumerable<Vector2D> Candidates(IShape moving, IShape other, double threshold, double minimumContact = MinimumContactMm)
    {
        var a = moving.Points;
        var b = other.Points;
        for (var i = 0; i < a.Count; i++)
        {
            var p = a[i];
            var q = a[(i + 1) % a.Count];
            var length = p.DistanceTo(q);
            if (length < minimumContact) continue;
            var u = new Vector2D(q.X - p.X, q.Y - p.Y).Normalized();
            var n = u.Perpendicular();
            for (var j = 0; j < b.Count; j++)
            {
                var r = b[j];
                var s = b[(j + 1) % b.Count];
                var v = new Vector2D(s.X - r.X, s.Y - r.Y);
                if (v.Length < minimumContact || Math.Abs(u.X * v.Y - u.Y * v.X) > 1e-6 * v.Length) continue;
                var delta = new Vector2D(r.X - p.X, r.Y - p.Y);
                var distance = delta.Dot(n);
                if (Math.Abs(distance) > threshold + 1e-6) continue;
                var t0 = delta.Dot(u);
                var t1 = new Vector2D(s.X - p.X, s.Y - p.Y).Dot(u);
                var overlap = Math.Min(length, Math.Max(t0, t1)) - Math.Max(0, Math.Min(t0, t1));
                if (overlap + 1e-6 >= minimumContact) yield return n * distance;
            }
        }
    }

    public static bool Connected(IShape a, IShape b)
    {
        var pa = PolygonGeometry.ToPolygon(a);
        var pb = PolygonGeometry.ToPolygon(b);
        return pa.Intersection(pb).Area > 1e-6 || Candidates(a, b, 1).Any();
    }
}

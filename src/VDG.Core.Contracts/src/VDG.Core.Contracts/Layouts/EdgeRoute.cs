using VDG.Core.Models;

namespace VDG.Core.Layouts;

/// <summary>Polyline routing for an edge.</summary>
public sealed class EdgeRoute
{
    public string EdgeId { get; }
    public IList<Point> Points { get; } = new List<Point>();

    public EdgeRoute(string edgeId) => EdgeId = edgeId ?? throw new ArgumentNullException(nameof(edgeId));
}

using VDG.Core.Models;

namespace VDG.Core.Drawing;

/// <summary>Command to draw a connector (edge) using a polyline path.</summary>
public sealed class DrawConnector : DrawCommand
{
    public string EdgeId { get; }
    public IList<Point> Points { get; } = new List<Point>();
    public string? Text { get; init; }
    public Models.ShapeStyle Style { get; init; } = Models.ShapeStyle.Default;

    public DrawConnector(string edgeId) => EdgeId = edgeId ?? throw new ArgumentNullException(nameof(edgeId));
}

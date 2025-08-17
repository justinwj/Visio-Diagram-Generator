using VDG.Core.Models;

namespace VDG.Core.Drawing;

/// <summary>Command to draw a shape (node) at a specific location.</summary>
public sealed class DrawShape : DrawCommand
{
    public string NodeId { get; }
    public Point Position { get; init; }
    public Size Size { get; init; }
    public ShapeStyle Style { get; init; } = ShapeStyle.Default;
    public string? Text { get; init; }

    public DrawShape(string nodeId, Point position, Size size)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Position = position;
        Size = size;
    }
}

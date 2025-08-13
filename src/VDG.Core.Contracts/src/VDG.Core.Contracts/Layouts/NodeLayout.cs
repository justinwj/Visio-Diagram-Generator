using VDG.Core.Models;

namespace VDG.Core.Layouts;

/// <summary>Resolved position & size for a node.</summary>
public sealed class NodeLayout
{
    public string NodeId { get; }
    public Point Position { get; set; }
    public Size Size { get; set; }

    public NodeLayout(string nodeId, Point position, Size size)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Position = position;
        Size = size;
    }
}

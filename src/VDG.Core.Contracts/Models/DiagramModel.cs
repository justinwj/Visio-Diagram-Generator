namespace VDG.Core.Models;

/// <summary>Container for nodes & edges that make up a diagram.</summary>
public sealed class DiagramModel
{
    public IList<Node> Nodes { get; }
    public IList<Edge> Edges { get; }
    public IDictionary<string, string> Metadata { get; }

    public DiagramModel(IEnumerable<Node>? nodes = null, IEnumerable<Edge>? edges = null)
    {
        Nodes = nodes is null ? new List<Node>() : new List<Node>(nodes);
        Edges = edges is null ? new List<Edge>() : new List<Edge>(edges);
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

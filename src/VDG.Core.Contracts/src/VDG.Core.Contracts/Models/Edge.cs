namespace VDG.Core.Models;

/// <summary>A diagram connector between two nodes.</summary>
public sealed class Edge
{
    public string Id { get; }
    public string SourceId { get; }
    public string TargetId { get; }
    public string? Label { get; set; }
    public bool Directed { get; init; } = true;
    public ShapeStyle Style { get; set; } = ShapeStyle.Default;
    public IDictionary<string, string> Metadata { get; }

    public Edge(string id, string sourceId, string targetId, string? label = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
        Label = label;
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

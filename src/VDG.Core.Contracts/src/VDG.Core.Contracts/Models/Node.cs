namespace VDG.Core.Models;

/// <summary>A diagram node (shape).</summary>
public sealed class Node
{
    public string Id { get; }
    public string Label { get; set; }
    public string? Type { get; set; }
    public ShapeStyle Style { get; set; } = ShapeStyle.Default;
    public Size? Size { get; set; }
    public IDictionary<string, string> Metadata { get; }

    public Node(string id, string label)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

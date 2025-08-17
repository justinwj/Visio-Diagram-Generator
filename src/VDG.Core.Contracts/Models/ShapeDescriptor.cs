namespace VDG.Core.Models;

/// <summary>Describes a drawable shape resource (e.g., a Visio master or a template key).</summary>
public sealed class ShapeDescriptor
{
    public string Key { get; }
    public string? Stencil { get; init; }
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

    public ShapeDescriptor(string key) => Key = key ?? throw new ArgumentNullException(nameof(key));
}

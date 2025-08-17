namespace VDG.Core.Layouts;

/// <summary>Layout results for nodes & edges.</summary>
public sealed class LayoutResult
{
    public IDictionary<string, NodeLayout> Nodes { get; } = new Dictionary<string, NodeLayout>();
    public IDictionary<string, EdgeRoute> Edges { get; } = new Dictionary<string, EdgeRoute>();
}

namespace VDG.Core.Models
{
    /// <summary>
    /// A directional edge between two items.
    /// </summary>
    public sealed record DiagramConnection(
        string FromId,
        string ToId,
        string? Label = null
    );
}
namespace VDG.Core.Models
{
    /// <summary>
    /// A single node placed on the diagram canvas.
    /// Use X/Y in page units (points or inches depending on upstream layout).
    /// </summary>
    public sealed record DiagramItem(
        string Id,
        string TypeName,
        string Label,
        double X,
        double Y
    );
}
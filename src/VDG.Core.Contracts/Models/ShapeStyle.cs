namespace VDG.Core.Models
{
    /// <summary>
    /// Describes basic visual styling for a shape. At this stage only a singleton Default style is provided.
    /// Additional properties can be added as needed (e.g. fill colour, line colour).
    /// </summary>
    public sealed class ShapeStyle
    {
        public static ShapeStyle Default { get; } = new ShapeStyle();

        // Future styling properties can be added here.
    }
}
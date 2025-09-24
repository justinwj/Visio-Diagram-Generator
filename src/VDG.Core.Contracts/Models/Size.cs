namespace VDG.Core.Models
{
    /// <summary>
    /// Represents the width and height of a shape in diagram units.
    /// </summary>
    public struct Size
    {
        public float Width { get; init; }
        public float Height { get; init; }

        public Size(float width, float height)
        {
            Width = width;
            Height = height;
        }
    }
}
using System;

namespace VDG.Core.Models
{
    /// <summary>
    /// Describes basic visual styling for a shape. Fill and stroke colours accept CSS style hex values
    /// ("#RRGGBB" or "#AARRGGBB"). LinePattern is a free-form string that can be mapped to Visio line styles.
    /// </summary>
    public sealed class ShapeStyle
    {
        public static ShapeStyle Default { get; } = new ShapeStyle();

        public string? FillColor { get; set; }
        public string? StrokeColor { get; set; }
        public string? LinePattern { get; set; }

        public bool IsDefault() => string.IsNullOrWhiteSpace(FillColor) &&
                                   string.IsNullOrWhiteSpace(StrokeColor) &&
                                   string.IsNullOrWhiteSpace(LinePattern);

        public ShapeStyle Clone() => new ShapeStyle
        {
            FillColor = FillColor,
            StrokeColor = StrokeColor,
            LinePattern = LinePattern,
        };
    }
}

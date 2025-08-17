namespace VDG.Core.Models;

/// <summary>Minimal shape/connector styling.</summary>
public sealed class ShapeStyle
{
    public string? FillColor { get; set; }
    public string? StrokeColor { get; set; }
    public double StrokeWidth { get; set; } = 1.0;
    public string? FontFamily { get; set; }
    public double FontSize { get; set; } = 11.0;

    public static readonly ShapeStyle Default = new();
}

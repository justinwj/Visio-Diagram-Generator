using VDG.Core.Models;

namespace VDG.Core.Layouts;

/// <summary>Options for the layout engine.</summary>
public sealed class LayoutOptions
{
    public double HorizontalSpacing { get; init; } = 42;
    public double VerticalSpacing { get; init; } = 42;
    public Size DefaultNodeSize { get; init; } = new(120, 60);
}

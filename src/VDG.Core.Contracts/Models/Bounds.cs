namespace VDG.Core.Models;

/// <summary>Axis-aligned rectangle.</summary>
public readonly record struct Bounds(Point Origin, Size Size)
{
    public double Left => Origin.X;
    public double Top => Origin.Y;
    public double Right => Origin.X + Size.Width;
    public double Bottom => Origin.Y + Size.Height;
}

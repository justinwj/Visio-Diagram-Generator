namespace VDG.Core.Models;

/// <summary>2D size in drawing units.</summary>
public readonly record struct Size(double Width, double Height)
{
    public static readonly Size Empty = new(0, 0);
}

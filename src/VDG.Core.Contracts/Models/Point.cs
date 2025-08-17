namespace VDG.Core.Models;

/// <summary>2D point in drawing units.</summary>
public readonly record struct Point(double X, double Y)
{
    public static readonly Point Zero = new(0, 0);
}

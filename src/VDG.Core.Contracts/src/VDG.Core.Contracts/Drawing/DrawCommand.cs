namespace VDG.Core.Drawing;

/// <summary>Base type for all drawing commands.</summary>
public abstract class DrawCommand
{
    /// <summary>Optional identifier for the command.</summary>
    public string? Id { get; init; }

    /// <summary>Optional human-readable note.</summary>
    public string? Description { get; init; }
}

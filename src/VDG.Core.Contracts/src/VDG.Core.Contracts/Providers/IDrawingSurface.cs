using VDG.Core.Drawing;

namespace VDG.Core.Providers;

/// <summary>Abstraction over a drawing target (e.g., Visio, SVG, canvas).</summary>
public interface IDrawingSurface
{
    /// <summary>Apply a drawing command to the surface.</summary>
    ValueTask ApplyAsync(DrawCommand command, CancellationToken cancellationToken = default);

    /// <summary>Flush any pending operations.</summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

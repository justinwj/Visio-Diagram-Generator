using VDG.Core.Models;

namespace VDG.Core.Layouts;

/// <summary>Computes positions & routes for a diagram model.</summary>
public interface ILayoutEngine
{
    Task<LayoutResult> ApplyAsync(DiagramModel model, LayoutOptions? options = null, CancellationToken cancellationToken = default);
}

using VDG.Core.Layouts;
using VDG.Core.Logging;
using VDG.Core.Models;
using VDG.Core.Providers;

namespace VDG.Core.Pipeline;

/// <summary>Mutable pipeline context passed between steps.</summary>
public sealed class PipelineContext
{
    public DiagramModel Model { get; set; }
    public LayoutResult? Layout { get; set; }
    public IDrawingSurface Surface { get; }
    public ILogger Logger { get; }
    public IShapeCatalog? Shapes { get; }
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
    public CancellationToken CancellationToken { get; set; }

    public PipelineContext(
        DiagramModel model,
        IDrawingSurface surface,
        ILogger? logger = null,
        IShapeCatalog? shapes = null,
        CancellationToken cancellationToken = default)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        Logger = logger ?? Logging.NullLogger.Instance;
        Shapes = shapes;
        CancellationToken = cancellationToken;
    }
}

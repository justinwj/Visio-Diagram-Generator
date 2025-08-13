using VDG.Core.Layouts;
using VDG.Core.Logging;
using VDG.Core.Models;
using VDG.Core.Pipeline;
using VDG.Core.Providers;

namespace VDG.Core.Builder;

/// <summary>High-level façade for wiring providers and running a small pipeline.</summary>
public sealed class DiagramBuilder
{
    public IModelProvider ModelProvider { get; }
    public IDrawingSurface Surface { get; }
    public ILayoutEngine LayoutEngine { get; }
    public IShapeCatalog? ShapeCatalog { get; init; }
    public ILogger Logger { get; init; } = Logging.NullLogger.Instance;
    public LayoutOptions? Options { get; init; }

    public DiagramBuilder(IModelProvider modelProvider, IDrawingSurface surface, ILayoutEngine layoutEngine)
    {
        ModelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        LayoutEngine = layoutEngine ?? throw new ArgumentNullException(nameof(layoutEngine));
    }

    /// <summary>Builds the diagram by validating, laying out, and rendering.</summary>
    public async Task BuildAsync(CancellationToken cancellationToken = default)
    {
        var model = await ModelProvider.GetModelAsync(cancellationToken).ConfigureAwait(false);

        var ctx = new PipelineContext(model, Surface, Logger, ShapeCatalog, cancellationToken);
        var pipeline = new DiagramPipeline()
            .Add(new Steps.ValidateModel())
            .Add(new Steps.RunLayout(LayoutEngine, Options))
            .Add(new Steps.Render());

        await pipeline.RunAsync(ctx, cancellationToken).ConfigureAwait(false);
    }
}

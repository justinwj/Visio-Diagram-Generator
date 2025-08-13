using VDG.Core.Layouts;
using VDG.Core.Logging;
using VDG.Core.Models;
using VDG.Core.Drawing;
using VDG.Core.Providers;

namespace VDG.Core.Pipeline;

/// <summary>Example placeholder steps; replace with real implementations later.</summary>
public static class Steps
{
    /// <summary>Ensures node/edge IDs are present and unique.</summary>
    public sealed class ValidateModel : IPipelineStep
    {
        public Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in context.Model.Nodes)
            {
                if (string.IsNullOrWhiteSpace(n.Id)) throw new InvalidOperationException("Node Id is required.");
                if (!ids.Add(n.Id)) throw new InvalidOperationException($"Duplicate node id: {n.Id}");
            }

            foreach (var e in context.Model.Edges)
            {
                if (string.IsNullOrWhiteSpace(e.Id)) throw new InvalidOperationException("Edge Id is required.");
                if (!ids.Add(e.Id)) throw new InvalidOperationException($"Duplicate edge id: {e.Id}");
            }

            context.Logger.Debug("Model validated.");
            return Task.CompletedTask;
        }
    }

    /// <summary>Runs the layout engine to produce positions and routes.</summary>
    public sealed class RunLayout : IPipelineStep
    {
        private readonly ILayoutEngine _engine;
        private readonly LayoutOptions? _options;

        public RunLayout(ILayoutEngine engine, LayoutOptions? options = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _options = options;
        }

        public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
        {
            context.Layout = await _engine.ApplyAsync(context.Model, _options, cancellationToken).ConfigureAwait(false);
            context.Logger.Debug("Layout computed.");
        }
    }

    /// <summary>Converts layout into drawing commands and sends them to the surface.</summary>
    public sealed class Render : IPipelineStep
    {
        public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
        {
            if (context.Layout is null)
            {
                context.Logger.Warn("No layout present; skipping render.");
                return;
            }

            // Draw nodes
            foreach (var kvp in context.Layout.Nodes)
            {
                var nl = kvp.Value;
                var cmd = new DrawShape(nl.NodeId, nl.Position, nl.Size);
                await context.Surface.ApplyAsync(cmd, cancellationToken).ConfigureAwait(false);
            }

            // Draw edges
            foreach (var kvp in context.Layout.Edges)
            {
                var er = kvp.Value;
                var cmd = new DrawConnector(er.EdgeId);
                foreach (var p in er.Points) cmd.Points.Add(p);
                await context.Surface.ApplyAsync(cmd, cancellationToken).ConfigureAwait(false);
            }

            await context.Surface.FlushAsync(cancellationToken).ConfigureAwait(false);
            context.Logger.Debug("Render complete.");
        }
    }
}

namespace VDG.Core.Pipeline;

/// <summary>Executes a sequence of steps to produce a diagram.</summary>
public sealed class DiagramPipeline
{
    private readonly IList<IPipelineStep> _steps = new List<IPipelineStep>();

    public DiagramPipeline Add(IPipelineStep step)
    {
        _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    public async Task RunAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        foreach (var step in _steps)
        {
            await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }
}

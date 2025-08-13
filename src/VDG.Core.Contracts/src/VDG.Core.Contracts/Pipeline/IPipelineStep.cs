namespace VDG.Core.Pipeline;

/// <summary>Represents a unit of work in the diagram build pipeline.</summary>
public interface IPipelineStep
{
    Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default);
}

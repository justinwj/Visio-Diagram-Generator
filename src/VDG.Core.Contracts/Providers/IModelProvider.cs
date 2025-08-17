using VDG.Core.Models;

namespace VDG.Core.Providers;

/// <summary>Supplies a diagram model from any source (memory, file, service, etc.).</summary>
public interface IModelProvider
{
    Task<DiagramModel> GetModelAsync(CancellationToken cancellationToken = default);
}

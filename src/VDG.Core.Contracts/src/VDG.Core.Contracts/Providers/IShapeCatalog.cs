using VDG.Core.Models;

namespace VDG.Core.Providers;

/// <summary>Resolves a node 'Type' (or key) to a <see cref=\"ShapeDescriptor\"/>.</summary>
public interface IShapeCatalog
{
    ShapeDescriptor? Resolve(string? shapeKey);
}

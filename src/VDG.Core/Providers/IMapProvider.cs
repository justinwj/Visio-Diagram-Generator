using System.Collections.Generic;
using VDG.Core.Models;

namespace VDG.Core.Providers
{
    /// <summary>
    /// Abstraction that supplies diagram nodes and edges to the pipeline.
    /// Pure contract; no COM or IO here.
    /// </summary>
    public interface IMapProvider
    {
        /// <summary>Return all diagram nodes/items to render.</summary>
        IReadOnlyList<DiagramItem> GetItems();

        /// <summary>Return all diagram connections/edges to render.</summary>
        IReadOnlyList<DiagramConnection> GetConnections();
    }
}
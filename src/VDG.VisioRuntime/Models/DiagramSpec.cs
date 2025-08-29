#nullable enable
using System.Collections.Generic;

namespace VDG.VisioRuntime.Models
{
    /// <summary>
    /// Top-level diagram specification object.
    /// </summary>
    public sealed class DiagramSpec
    {
        /// <summary>Optional list of stencils to preload (paths or names).</summary>
        public string[]? Stencils { get; set; }

        /// <summary>Nodes (shapes) to draw.</summary>
        public List<NodeSpec>? Nodes { get; set; }

        /// <summary>Edges (connectors) to draw.</summary>
        public List<EdgeSpec>? Edges { get; set; }
    }

    /// <summary>
    /// Node (shape) specification.
    /// </summary>
    public sealed class NodeSpec
    {
        public string Id { get; set; } = string.Empty;
        public string? Kind { get; set; }
        public string? Master { get; set; }
        public string? Stencil { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double W { get; set; } = 2.0;
        public double H { get; set; } = 1.0;
        public string? Text { get; set; }
    }

    /// <summary>
    /// Edge (connector) specification.
    /// </summary>
    public sealed class EdgeSpec
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? Kind { get; set; }
    }
}

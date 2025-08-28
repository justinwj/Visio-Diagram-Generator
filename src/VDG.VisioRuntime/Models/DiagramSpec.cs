using System.Collections.Generic;

namespace VDG.VisioRuntime.Models
{
    /// <summary>
    /// Top‑level specification for a diagram defined in JSON.  A diagram
    /// consists of zero or more stencils, a collection of nodes and a
    /// collection of edges connecting the nodes.
    /// </summary>
    public sealed class DiagramSpec
    {
        /// <summary>
        /// Optional list of stencil names or paths to load before
        /// rendering nodes.  If omitted, the renderer will resolve
        /// masters through fallback logic.
        /// </summary>
        public string[]? Stencils { get; set; }

        /// <summary>
        /// The collection of nodes.  Each node must have a unique
        /// identifier.  Coordinates are optional – if omitted an
        /// auto‑layout will assign positions.
        /// </summary>
        public List<NodeSpec> Nodes { get; set; } = new();

        /// <summary>
        /// The collection of edges connecting nodes.  The
        /// <see cref="EdgeSpec.From"/> and <see cref="EdgeSpec.To"/> fields
        /// refer to node identifiers.
        /// </summary>
        public List<EdgeSpec> Edges { get; set; } = new();
    }

    /// <summary>
    /// Specification for a single node in a diagram.  Nodes can be
    /// rendered either via a stencil master (preferred) or via a
    /// built‑in shape kind.  Coordinates are measured in inches.
    /// </summary>
    public sealed class NodeSpec
    {
        public string Id { get; set; } = string.Empty;
        public string? Text { get; set; }
        {
            get;
            set;
        }
        public string? Master { get; set; }
        {
            get;
            set;
        }
        public string? Kind { get; set; }
        {
            get;
            set;
        }
        public string? Stencil { get; set; }
        {
            get;
            set;
        }
        public double? X { get; set; }
        {
            get;
            set;
        }
        public double? Y { get; set; }
        {
            get;
            set;
        }
        public double W { get; set; } = 2.0;
        public double H { get; set; } = 1.0;
    }

    /// <summary>
    /// Specification for an edge (connector) between two nodes.  Edges
    /// reference the identifiers of their source and target nodes.  A
    /// label may be associated with the connector.
    /// </summary>
    public sealed class EdgeSpec
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string? Label { get; set; }
        {
            get;
            set;
        }
        public string? Kind { get; set; }
        {
            get;
            set;
        }
    }
}
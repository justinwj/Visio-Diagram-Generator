namespace VisioDiagramGenerator.Algorithms

open System
open VDG.Core.Models

/// A point in two-dimensional space represented by single precision floats.
[<CLIMutable>]
type PointF = { X: float32; Y: float32 }

/// Carries position and optional size for a node in a layout.
[<CLIMutable>]
type NodeLayout =
    { Id: string
      Position: PointF
      Size: Nullable<Size> }

/// Describes a polyline route for an edge. Points are expected to include
/// at least a start and end point.
[<CLIMutable>]
type EdgeRoute =
    { Id: string
      Points: PointF array }

/// The result of a layout operation, containing layouts for nodes and edges.
[<CLIMutable>]
type LayoutResult =
    { Nodes: NodeLayout array
      Edges: EdgeRoute array }

namespace VisioDiagramGenerator.Algorithms

open VDG.Core.Models

/// A point in two-dimensional space represented by single precision floats.
type PointF = { X: float32; Y: float32 }

/// Carries position and optional size for a node in a layout.
type NodeLayout = { Id: string; Position: PointF; Size: Size option }

/// Describes a polyline route for an edge. Points are expected to include
/// at least a start and end point.
type EdgeRoute = { Id: string; Points: PointF list }

/// The result of a layout operation, containing layouts for nodes and edges.
type LayoutResult = { Nodes: NodeLayout list; Edges: EdgeRoute list }
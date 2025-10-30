namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic
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
      Points: PointF array
      LabelPoints: PointF array }

[<CLIMutable>]
type NodeModuleAssignment =
    { NodeId: string
      ModuleId: string }

[<CLIMutable>]
type PageLayoutInfo =
    { PageIndex: int
      Origin: PointF
      Width: float32
      Height: float32
      BodyHeight: float32 }

/// Axis-aligned rectangle helper used for container bounds.
[<CLIMutable>]
type RectangleF =
    { Left: float32
      Bottom: float32
      Width: float32
      Height: float32 }

/// Captures visible container geometry emitted by the layout planner.
[<CLIMutable>]
type ContainerLayout =
    { Id: string
      Label: string
      Tier: string
      Bounds: RectangleF
      VisibleNodes: int
      OverflowCount: int }

/// Aggregated overflow metadata for nodes trimmed from a container/module.
[<CLIMutable>]
type LayoutOverflow =
    { ContainerId: string
      HiddenNodeCount: int }

/// Aggregated statistics for a computed layout.
[<CLIMutable>]
type LayoutStats =
    { NodeCount: int
      ConnectorCount: int
      ModuleCount: int
      ContainerCount: int
      ModuleIds: string array
      Overflows: LayoutOverflow array }

/// Planner output describing how modules map to a physical page.
[<CLIMutable>]
type PagePlan =
    { PageIndex: int
      Modules: string array
      Connectors: int
      Nodes: int
      Occupancy: float }

[<CLIMutable>]
type LayerPlan =
    { LayerIndex: int
      Modules: string array
      ShapeCount: int
      ConnectorCount: int }

[<CLIMutable>]
type LayerBridge =
    { BridgeId: string
      SourceLayer: int
      SourceNodeId: string
      TargetLayer: int
      TargetNodeId: string
      ConnectorId: string
      Metadata: IDictionary<string, string>
      EntryAnchor: PointF
      ExitAnchor: PointF }

[<CLIMutable>]
type PageBridge =
    { BridgeId: string
      SourcePage: int
      TargetPage: int
      SourceModuleId: string
      TargetModuleId: string
      SourceNodeId: string
      TargetNodeId: string
      ConnectorId: string
      Metadata: IDictionary<string, string>
      EntryAnchor: PointF
      ExitAnchor: PointF }

/// High-level layout plan returned by the algorithms layer.
[<CLIMutable>]
type LayoutPlan =
    { OutputMode: string
      CanvasWidth: float32
      CanvasHeight: float32
      PageHeight: float32
      PageMargin: float32
      TitleHeight: float32
      Nodes: NodeLayout array
      NodeModules: NodeModuleAssignment array
      PageLayouts: PageLayoutInfo array
      Containers: ContainerLayout array
      Edges: EdgeRoute array
      Pages: PagePlan array
      Layers: LayerPlan array
      Bridges: LayerBridge array
      PageBridges: PageBridge array
      Stats: LayoutStats }

/// The result of a layout operation, containing layouts for nodes and edges.
[<CLIMutable>]
type LayoutResult =
    { Nodes: NodeLayout array
      Edges: EdgeRoute array }

namespace VDG.Core

/// Represents a 2D coordinate on a page.
type Point = float * float

/// Abstract layout strategy for positioning nodes on a page.
type ILayout =
    /// Given a list of nodes, returns a map of node id to page coordinates.
    abstract member Arrange: Node list -> Map<string, Point>

/// Simple grid layout for positioning nodes.  Nodes are placed left‑to‑right,
/// top‑to‑bottom in a uniform grid.  The spacing between items can be configured
/// via the constructor argument.
type GridLayout(gridSpacing: float) =
    interface ILayout with
        member _.Arrange(nodes) =
            nodes
            |> List.indexed
            |> List.map (fun (i, n) ->
                let x = float (i % 5) * gridSpacing
                let y = float (i / 5) * gridSpacing
                n.Id, (x, y))
            |> Map.ofList

/// Abstracts the underlying Visio automation service.  Implementations of this interface
/// should handle creating shapes, connectors and resizing the page.
type IVisioService =
    /// Adds a rectangular shape at the given position with size and text.
    /// Returns an identifier for the shape that can be used for connectors.
    abstract member AddRectangle : float * float * float * float * string -> string
    /// Adds a connector between two existing shapes.  A label may be supplied.
    abstract member AddConnector : string * string * string option -> unit
    /// Resizes the page to the given dimensions.
    abstract member ResizePage : float * float -> unit

/// Executes a list of commands against a Visio service using a layout to determine positions.
module DiagramBuilder =
    /// Execute all build commands.  Shapes are created first using the layout, then connectors.
    let execute (service: IVisioService) (layout: ILayout) (commands: Command list) =
        // Extract node commands and determine positions.
        let nodes =
            commands
            |> List.choose (function
                | AddNode n -> Some n
                | _ -> None)
        let positions = layout.Arrange nodes
        // Create shapes and map ids to Visio shape ids.
        let shapeIdMap =
            nodes
            |> List.map (fun n ->
                let x, y = positions.[n.Id]
                // Default shape size; could be parameterised in config.
                let shapeId = service.AddRectangle(x, y, 2.0, 1.0, n.Label)
                n.Id, shapeId)
            |> Map.ofList
        // Create connectors after all shapes exist.
        for cmd in commands do
            match cmd with
            | AddConnector c ->
                match shapeIdMap.TryFind c.From, shapeIdMap.TryFind c.To with
                | Some fromShape, Some toShape ->
                    service.AddConnector(fromShape, toShape, c.Label)
                | _ ->
                    // ignore connectors that reference unknown nodes
                    ()
            | _ -> ()
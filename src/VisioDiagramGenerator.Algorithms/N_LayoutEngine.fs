namespace VisioDiagramGenerator.Algorithms

open VDG.Core.Models

/// <summary>
/// Provides a simple deterministic layout for diagrams. Nodes are spaced evenly along
/// the X-axis and edges are routed as straight lines between node centres. This is
/// intended as a starting point and can be replaced with more sophisticated
/// algorithms later.
/// </summary>
module LayoutEngine =

    /// Computes a layout for the given model. Nodes are placed horizontally
    /// in the order they appear in the model. Each subsequent node is offset
    /// by two units along the X-axis. Y-coordinates are fixed at 0.
    let compute (model : DiagramModel) : LayoutResult =
        // Create node layouts spaced evenly along the X-axis
        let nodeLayouts =
            model.Nodes
            |> Seq.mapi (fun i node ->
                let x = float32 i * 2.0f
                let y = 0.0f
                { Id = node.Id
                  Position = { X = x; Y = y }
                  Size = node.Size })
            |> Seq.toList
        // Build a lookup for node positions
        let lookup = nodeLayouts |> List.map (fun nl -> nl.Id, nl) |> Map.ofList
        // Create straight-line routes for each edge
        let edgeRoutes =
            model.Edges
            |> Seq.map (fun edge ->
                match Map.tryFind edge.SourceId lookup, Map.tryFind edge.TargetId lookup with
                | Some src, Some dst ->
                    { Id = edge.Id
                      Points = [ src.Position; dst.Position ] }
                | _ ->
                    // If either node is missing, produce an empty route
                    { Id = edge.Id; Points = [] })
            |> Seq.toList
        { Nodes = nodeLayouts; Edges = edgeRoutes }
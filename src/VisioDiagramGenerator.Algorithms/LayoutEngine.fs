namespace VisioDiagramGenerator.Algorithms

open System
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
    let compute (model: DiagramModel): LayoutResult =
        let nodeLayouts =
            model.Nodes
            |> Seq.mapi (fun i node ->
                let x = float32 i * 2.0f
                let position = { X = x; Y = 0.0f }
                { Id = node.Id
                  Position = position
                  Size = node.Size })
            |> Seq.toArray

        let lookup =
            nodeLayouts
            |> Array.map (fun nl -> nl.Id, nl)
            |> Map.ofArray

        let edgeRoutes =
            model.Edges
            |> Seq.map (fun edge ->
                match Map.tryFind edge.SourceId lookup, Map.tryFind edge.TargetId lookup with
                | Some src, Some dst ->
                    { Id = edge.Id
                      Points = [| src.Position; dst.Position |] }
                | _ ->
                    // If either node is missing, produce an empty route
                    { Id = edge.Id; Points = Array.empty })
            |> Seq.toArray

        { Nodes = nodeLayouts
          Edges = edgeRoutes }

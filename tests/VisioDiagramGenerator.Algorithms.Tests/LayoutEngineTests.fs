module VisioDiagramGenerator.Algorithms.Tests.LayoutEngineTests

open Xunit
open VDG.Core.Models
open VisioDiagramGenerator.Algorithms

[<Fact>]
let Compute_ReturnsLayoutWithNodesAndEdges () =
    let n1 = Node("A", "A")
    let n2 = Node("B", "B")
    let n3 = Node("C", "C")
    let e1 = Edge("e1", "A", "B", null)
    let e2 = Edge("e2", "B", "C", null)
    let model = DiagramModel([| n1; n2; n3 |], [| e1; e2 |])
    let result = LayoutEngine.compute model
    Assert.Equal(3, result.Nodes.Length)
    Assert.Equal(2, result.Edges.Length)
    let ys = result.Nodes |> Array.map (fun nl -> nl.Position.Y)
    let vertical = ys |> Array.pairwise |> Array.forall (fun (y1, y2) -> y2 > y1)
    Assert.True(vertical)

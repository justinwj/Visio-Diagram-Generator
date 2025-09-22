namespace VisioDiagramGenerator.Algorithms.Tests

open Xunit
open VDG.Core.Models
open VisioDiagramGenerator.Algorithms

type N_LayoutEngineTests() =
    [<Fact>]
    member _.Compute_ReturnsLayoutWithNodesAndEdges() =
        let n1 = Node("A", "A")
        let n2 = Node("B", "B")
        let n3 = Node("C", "C")
        let e1 = Edge("e1", "A", "B", null)
        let e2 = Edge("e2", "B", "C", null)
        let model = DiagramModel([| n1; n2; n3 |], [| e1; e2 |])
        let result = LayoutEngine.compute model
        Assert.Equal(3, result.Nodes.Length)
        Assert.Equal(2, result.Edges.Length)
        // Verify that nodes have increasing X coordinates
        let xs = result.Nodes |> List.map (fun nl -> nl.Position.X)
        let increasing = xs |> List.pairwise |> List.forall (fun (x, y) -> y > x)
        Assert.True(increasing)
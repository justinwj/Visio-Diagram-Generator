module VisioDiagramGenerator.Algorithms.Tests.ViewModePlannerTests

open Xunit
open VDG.Core.Models
open VisioDiagramGenerator.Algorithms

let private nodeId idx = sprintf "n%02d" idx

let private mkNode moduleId tier idx =
    let id = nodeId idx
    let node = Node(id, sprintf "Node %02d" idx)
    node.GroupId <- moduleId
    node.Tier <- tier
    node

[<Fact>]
let ``computeViewLayout splits large module into numbered segments`` () =
    let nodes =
        [| for i in 1 .. 25 -> mkNode "ModuleA" "Services" i |]
    let model = DiagramModel(nodes, Array.empty)
    let plan = ViewModePlanner.computeViewLayout model

    let containerIds =
        plan.Containers
        |> Array.map (fun c -> c.Id)
        |> Set.ofArray

    Assert.True(containerIds.Contains "ModuleA#part1")
    Assert.True(containerIds.Contains "ModuleA#part2")
    Assert.True(containerIds.Contains "ModuleA#part3")

    let visibleCounts =
        plan.Containers
        |> Array.filter (fun c -> c.Id.StartsWith("ModuleA"))
        |> Array.map (fun c -> c.VisibleNodes)

    Assert.True(visibleCounts |> Array.forall (fun count -> count <= 12))

    let partOne =
        plan.Containers
        |> Array.find (fun c -> c.Id = "ModuleA#part1")

    Assert.Contains("part 1/3", partOne.Label)
    Assert.Contains("ModuleA#part1", plan.Stats.ModuleIds)

[<Fact>]
let ``page plans honour module caps and bridge cross-page segments`` () =
    let nodes =
        [| for i in 1 .. 18 -> mkNode "ModuleA" "Services" i |]
    let edges =
        [| Edge("e1", nodeId 1, nodeId 18) |]
    let model = DiagramModel(nodes, edges)
    model.Metadata["layout.page.plan.maxModulesPerPage"] <- "1"

    let plan = ViewModePlanner.computeViewLayout model

    Assert.Equal(2, plan.Pages.Length)
    Assert.All(plan.Pages, fun page -> Assert.Equal(1, page.Modules.Length))

    let moduleToPage =
        plan.Pages
        |> Array.collect (fun page -> page.Modules |> Array.map (fun m -> m, page.PageIndex))
        |> dict

    Assert.Equal(0, moduleToPage["ModuleA#part1"])
    Assert.Equal(1, moduleToPage["ModuleA#part2"])

    let bridge = Assert.Single plan.PageBridges
    Assert.Equal("ModuleA#part1", bridge.SourceModuleId)
    Assert.Equal("ModuleA#part2", bridge.TargetModuleId)
    Assert.Equal(nodeId 1, bridge.SourceNodeId)
    Assert.Equal(nodeId 18, bridge.TargetNodeId)
    Assert.Equal(0, bridge.SourcePage)
    Assert.Equal(1, bridge.TargetPage)

[<Fact>]
let ``long labels expand node layouts`` () =
    let nodes =
        [| for i in 1 .. 3 ->
               let node = mkNode "ModuleWide" "Services" i
               node.Label <- "VeryLongProcedureIdentifierNameWhichNeedsMoreWidth" + string i
               node |]
    let model = DiagramModel(nodes, Array.empty)

    let plan = ViewModePlanner.computeViewLayout model

    let sampleNode =
        plan.Nodes
        |> Array.find (fun layout -> layout.Id = nodeId 1)

    Assert.True(sampleNode.Size.HasValue)
    Assert.True(sampleNode.Size.Value.Width > 1.5f)

    let container =
        plan.Containers
        |> Array.find (fun c -> c.Id = "ModuleWide")

    Assert.True(container.Bounds.Width > 2.1f)

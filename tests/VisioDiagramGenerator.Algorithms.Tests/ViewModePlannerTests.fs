module VisioDiagramGenerator.Algorithms.Tests.ViewModePlannerTests

open System
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

    let visibleCounts =
        plan.Containers
        |> Array.filter (fun c -> c.Id.StartsWith("ModuleA"))
        |> Array.map (fun c -> c.VisibleNodes)

    Assert.True(visibleCounts |> Array.forall (fun count -> count <= 15))

    let partOne =
        plan.Containers
        |> Array.find (fun c -> c.Id = "ModuleA#part1")

    Assert.Contains("part 1/2", partOne.Label)
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

[<Fact>]
let ``lane capacity splits modules into additional pages`` () =
    let nodes =
        [| for i in 1 .. 18 ->
               let moduleIndex = ((i - 1) / 3) + 1
               let moduleId = sprintf "Module%02d" moduleIndex
               mkNode moduleId "Services" i |]
    let model = DiagramModel(nodes, Array.empty)
    model.Metadata["layout.view.maxModulesPerLane"] <- "2"
    model.Metadata["layout.page.plan.maxModulesPerPage"] <- "10"

    let plan = ViewModePlanner.computeViewLayout model
    let orderedPages =
        plan.Pages
        |> Array.sortBy (fun page -> page.PageIndex)

    Assert.True(orderedPages.Length >= 3)
    Assert.True(orderedPages.Length <= 6)
    Assert.All(orderedPages, fun page -> Assert.InRange(page.Modules.Length, 1, 2))

    let moduleAssignments =
        orderedPages
        |> Array.collect (fun page ->
            page.Modules
            |> Array.map (fun moduleId -> moduleId, page.PageIndex))
        |> dict

    let flattened =
        orderedPages
        |> Array.collect (fun page -> page.Modules)

    Assert.Equal<string array>(
        [| "Module01"; "Module02"; "Module03"; "Module04"; "Module05"; "Module06" |],
        flattened)

    for i = 1 to 5 do
        Assert.True(moduleAssignments[sprintf "Module%02d" (i + 1)] >= moduleAssignments[sprintf "Module%02d" i])

    Assert.True(plan.RowLayouts.Length > 0)
    Assert.All(plan.RowLayouts, fun row -> Assert.True(row.Top > row.Bottom))

[<Fact>]
let ``channel labels aggregate corridor callouts`` () =
    let nodes =
        [| mkNode "ModuleA" "Modules" 1
           mkNode "ModuleA" "Modules" 2
           mkNode "ModuleB" "Modules" 3 |]
    let e1 = Edge("edge1", nodeId 1, nodeId 3)
    e1.Label <- "Alpha"
    let e2 = Edge("edge2", nodeId 2, nodeId 3)
    e2.Label <- "Beta"
    let edges = [| e1; e2 |]
    let model = DiagramModel(nodes, edges)

    let plan = ViewModePlanner.computeViewLayout model

    match plan.ChannelLabels |> Array.tryFind (fun ch -> ch.Key.Contains("ModuleA", StringComparison.OrdinalIgnoreCase)) with
    | Some channel ->
        Assert.Contains("Alpha", channel.Lines)
        Assert.Contains("Beta", channel.Lines)
    | None ->
        Assert.True(false, "Expected channel label was not generated")

[<Fact>]
let ``row segmentation honours lane module caps even with wide slot setting`` () =
    let nodes =
        [| for moduleIdx in 1 .. 8 ->
               mkNode (sprintf "Module%02d" moduleIdx) "Services" moduleIdx |]
    let model = DiagramModel(nodes, Array.empty)
    model.Metadata["layout.view.slotsPerRow"] <- "24"
    model.Metadata["layout.view.maxModulesPerLane"] <- "3"
    model.Metadata["layout.page.plan.maxModulesPerPage"] <- "24"

    let plan = ViewModePlanner.computeViewLayout model

    Assert.True(plan.Pages.Length >= 1)
    let tierRows =
        plan.RowLayouts
        |> Array.filter (fun row -> row.Tier.Equals("Services", StringComparison.OrdinalIgnoreCase))
        |> Array.sortBy (fun row -> row.TierRowIndex)

    Assert.Equal(3, tierRows.Length)
    Assert.Equal(0, tierRows[0].TierRowIndex)
    Assert.Equal(1, tierRows[1].TierRowIndex)
    Assert.Equal(2, tierRows[2].TierRowIndex)

[<Fact>]
let ``row segmentation honours lane node caps before pagination`` () =
    let nodes =
        [| for moduleIdx in 0 .. 3 do
               for nodeIdx in 1 .. 40 ->
                   let globalIdx = moduleIdx * 40 + nodeIdx
                   mkNode (sprintf "Module%02d" (moduleIdx + 1)) "Services" globalIdx |]
    let model = DiagramModel(nodes, Array.empty)
    model.Metadata["layout.view.slotsPerRow"] <- "50"
    model.Metadata["layout.view.maxModulesPerLane"] <- "50"
    model.Metadata["layout.view.maxNodesPerLane"] <- "80"
    model.Metadata["layout.page.plan.maxModulesPerPage"] <- "50"

    let plan = ViewModePlanner.computeViewLayout model

    Assert.Equal(1, plan.Pages.Length)
    let tierRows =
        plan.RowLayouts
        |> Array.filter (fun row -> row.Tier.Equals("Services", StringComparison.OrdinalIgnoreCase))
        |> Array.sortBy (fun row -> row.TierRowIndex)

    Assert.Equal(2, tierRows.Length)
    Assert.Equal(0, tierRows[0].TierRowIndex)
    Assert.Equal(1, tierRows[1].TierRowIndex)
    Assert.True(tierRows[0].Height > 0.f)
    Assert.True(tierRows[1].Top < tierRows[0].Top)

[<Fact>]
let ``edge routes emit channel metadata`` () =
    let nodes =
        [| mkNode "ModuleA" "Services" 1
           mkNode "ModuleA" "Services" 2
           mkNode "ModuleB" "Services" 3 |]
    let edges =
        [| Edge("loop", nodeId 1, nodeId 2)
           Edge("cross", nodeId 1, nodeId 3) |]
    let model = DiagramModel(nodes, edges)

    let plan = ViewModePlanner.computeViewLayout model
    let loopRoute =
        plan.Edges
        |> Array.find (fun route -> route.Id = "loop")
    Assert.True(loopRoute.Channel.IsSome)
    let loopChannel = loopRoute.Channel.Value
    Assert.Equal("ModuleA", loopChannel.SourceModuleId)
    Assert.Equal("ModuleA", loopChannel.TargetModuleId)
    Assert.Equal("self:ModuleA", loopChannel.Key)

    let crossRoute =
        plan.Edges
        |> Array.find (fun route -> route.Id = "cross")
    Assert.True(crossRoute.Channel.IsSome)
    let crossChannel = crossRoute.Channel.Value
    Assert.Equal("ModuleA", crossChannel.SourceModuleId)
    Assert.Equal("ModuleB", crossChannel.TargetModuleId)
    Assert.False(String.IsNullOrWhiteSpace crossChannel.Key)

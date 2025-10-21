module VisioDiagramGenerator.Algorithms.Tests.PagingPlannerTests

open Xunit
open VisioDiagramGenerator.Algorithms

let private mkSpanModule moduleId connectors occupancy nodeCount spanMin spanMax height =
    { ModuleStats.ModuleId = moduleId
      ConnectorCount = connectors
      NodeCount = nodeCount
      OccupancyPercent = occupancy
      HeightEstimate = height
      SpanMin = spanMin
      SpanMax = spanMax
      HasSpan = true
      CrossModuleConnectors = 0 }

let private mkLooseModule moduleId connectors occupancy nodeCount height =
    { ModuleStats.ModuleId = moduleId
      ConnectorCount = connectors
      NodeCount = nodeCount
      OccupancyPercent = occupancy
      HeightEstimate = height
      SpanMin = 0.0
      SpanMax = 0.0
      HasSpan = false
      CrossModuleConnectors = 0 }

[<Fact>]
let ``modules with overlapping spans stay on same page`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkSpanModule "A" 2 100.0 6 0.0 7.5 7.5
               mkSpanModule "B" 3 100.0 5 0.2 7.4 7.4 |] }

    let options =
        { PageSplitOptions.MaxConnectors = 400
          MaxOccupancyPercent = 95.0
          LaneSplitAllowed = false
          MaxPageHeightIn = 7.5
          MaxModulesPerPage = 12
          HeightSlackPercent = 10.0 }

    let plans = PagingPlanner.computePages options dataset
    let single = Assert.Single plans
    Assert.Equal<string array>([| "A"; "B" |], single.Modules)
    Assert.Equal(5, single.Connectors)
    Assert.Equal(11, single.Nodes)
    Assert.Equal(100.0, single.Occupancy, 1)

[<Fact>]
let ``computePages splits when connector threshold exceeded`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkSpanModule "A" 3 35.0 6 0.0 2.0 2.5
               mkSpanModule "B" 4 40.0 5 0.0 2.0 2.5
               mkSpanModule "C" 2 20.0 3 0.0 2.0 2.5 |] }

    let options =
        { PageSplitOptions.MaxConnectors = 5
          MaxOccupancyPercent = 100.0
          LaneSplitAllowed = false
          MaxPageHeightIn = 10.0
          MaxModulesPerPage = 10
          HeightSlackPercent = 5.0 }

    let plans = PagingPlanner.computePages options dataset

    Assert.Equal(3, plans.Length)
    Assert.Equal<string array>([| "A" |], plans[0].Modules)
    Assert.Equal(3, plans[0].Connectors)
    Assert.Equal(6, plans[0].Nodes)
    Assert.Equal<string array>([| "B" |], plans[1].Modules)
    Assert.Equal(4, plans[1].Connectors)
    Assert.Equal(5, plans[1].Nodes)
    Assert.Equal<string array>([| "C" |], plans[2].Modules)
    Assert.Equal(2, plans[2].Connectors)
    Assert.Equal(3, plans[2].Nodes)

[<Fact>]
let ``computePages splits when height limit exceeded`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkSpanModule "A" 2 40.0 4 0.0 4.0 4.5
               mkSpanModule "B" 2 45.0 4 4.2 8.0 4.2
               mkSpanModule "C" 2 20.0 2 8.1 12.3 4.5 |] }

    let options =
        { PageSplitOptions.MaxConnectors = 100
          MaxOccupancyPercent = 95.0
          LaneSplitAllowed = false
          MaxPageHeightIn = 8.0
          MaxModulesPerPage = 10
          HeightSlackPercent = 5.0 }

    let plans = PagingPlanner.computePages options dataset
    Assert.Equal(2, plans.Length)
    Assert.Equal<string array>([| "A"; "B" |], plans[0].Modules)
    Assert.Equal<string array>([| "C" |], plans[1].Modules)
    Assert.True(plans[0].Occupancy > plans[1].Occupancy)

[<Fact>]
let ``computePages falls back to occupancy when height unavailable`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkLooseModule "A" 1 60.0 4 3.0
               mkLooseModule "B" 1 60.0 4 3.0
               mkLooseModule "C" 1 60.0 4 3.0 |] }

    let options =
        { PageSplitOptions.MaxConnectors = 100
          MaxOccupancyPercent = 100.0
          LaneSplitAllowed = false
          MaxPageHeightIn = 0.0
          MaxModulesPerPage = 10
          HeightSlackPercent = 0.0 }

    let plans = PagingPlanner.computePages options dataset
    Assert.Equal(3, plans.Length)
    Assert.Equal<string array>([| "A" |], plans[0].Modules)
    Assert.Equal<string array>([| "B" |], plans[1].Modules)
    Assert.Equal<string array>([| "C" |], plans[2].Modules)

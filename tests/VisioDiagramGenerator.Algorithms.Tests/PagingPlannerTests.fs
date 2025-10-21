module VisioDiagramGenerator.Algorithms.Tests.PagingPlannerTests

open Xunit
open VisioDiagramGenerator.Algorithms

let private mkModule moduleId connectors occupancy nodeCount =
    { ModuleStats.ModuleId = moduleId
      ConnectorCount = connectors
      NodeCount = nodeCount
      OccupancyPercent = occupancy }

[<Fact>]
let ``computePages splits when connector threshold exceeded`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkModule "A" 3 35.0 6
               mkModule "B" 4 40.0 5
               mkModule "C" 2 20.0 3 |] }

    let options =
        { PageSplitOptions.MaxConnectors = 5
          MaxOccupancyPercent = 100.0
          LaneSplitAllowed = false }

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
let ``computePages respects occupancy threshold when lane splitting disabled`` () =
    let dataset =
        { DiagramDataset.Modules =
            [| mkModule "A" 2 40.0 4
               mkModule "B" 2 70.0 3
               mkModule "C" 2 95.0 2 |] }

    let strictOptions =
        { PageSplitOptions.MaxConnectors = 10
          MaxOccupancyPercent = 80.0
          LaneSplitAllowed = false }

    let relaxedOptions = { strictOptions with LaneSplitAllowed = true }

    let strictPlans = PagingPlanner.computePages strictOptions dataset
    Assert.Equal(3, strictPlans.Length)
    Assert.Equal<string array>([| "A" |], strictPlans[0].Modules)
    Assert.Equal(2, strictPlans[0].Connectors)
    Assert.Equal(40.0, strictPlans[0].Occupancy)
    Assert.Equal(4, strictPlans[0].Nodes)
    Assert.Equal<string array>([| "B" |], strictPlans[1].Modules)
    Assert.Equal(2, strictPlans[1].Connectors)
    Assert.Equal(70.0, strictPlans[1].Occupancy)
    Assert.Equal(3, strictPlans[1].Nodes)
    Assert.Equal<string array>([| "C" |], strictPlans[2].Modules)
    Assert.Equal(2, strictPlans[2].Connectors)
    Assert.Equal(95.0, strictPlans[2].Occupancy)
    Assert.Equal(2, strictPlans[2].Nodes)

    let relaxedPlans = PagingPlanner.computePages relaxedOptions dataset
    let relaxedPlan = Assert.Single relaxedPlans
    Assert.Equal<string array>([| "A"; "B"; "C" |], relaxedPlan.Modules)
    Assert.Equal(6, relaxedPlan.Connectors)
    Assert.Equal(205.0, relaxedPlan.Occupancy)
    Assert.Equal(9, relaxedPlan.Nodes)

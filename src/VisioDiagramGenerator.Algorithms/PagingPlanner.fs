namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic

[<CLIMutable>]
type ModuleStats =
    { ModuleId: string
      ConnectorCount: int
      NodeCount: int
      OccupancyPercent: float }

[<CLIMutable>]
type DiagramDataset =
    { Modules: ModuleStats array }

[<CLIMutable>]
type PageSplitOptions =
    { MaxConnectors: int
      MaxOccupancyPercent: float
      LaneSplitAllowed: bool }

[<CLIMutable>]
type PagePlan =
    { PageIndex: int
      Modules: string array
      Connectors: int
      Nodes: int
      Occupancy: float }

module PagingPlanner =

    let private sanitizeModuleId (moduleId: string) =
        if String.IsNullOrWhiteSpace moduleId then String.Empty
        else moduleId.Trim()

    let private sanitizeModules (modules: ModuleStats array) =
        if isNull (box modules) then
            Array.empty
        else
            modules
            |> Array.filter (fun m -> not (isNull (box m)))
            |> Array.map (fun m ->
                let moduleId = sanitizeModuleId m.ModuleId
                let connectors = if m.ConnectorCount < 0 then 0 else m.ConnectorCount
                let nodeCount = if m.NodeCount < 0 then 0 else m.NodeCount
                let occupancy =
                    if Double.IsNaN m.OccupancyPercent then 0.0
                    else max 0.0 m.OccupancyPercent
                { ModuleId = moduleId
                  ConnectorCount = connectors
                  NodeCount = nodeCount
                  OccupancyPercent = occupancy })

    let private normalizeOptions (thresholds: PageSplitOptions) =
        let maxConnectors =
            if thresholds.MaxConnectors <= 0 then Int32.MaxValue
            else thresholds.MaxConnectors

        let occupancyLimit =
            let raw = thresholds.MaxOccupancyPercent
            if thresholds.LaneSplitAllowed then Double.PositiveInfinity
            elif Double.IsNaN raw || raw <= 0.0 then Double.PositiveInfinity
            else raw

        maxConnectors, occupancyLimit

    let computePages (thresholds: PageSplitOptions) (dataset: DiagramDataset) =
        if isNull (box dataset) then invalidArg (nameof dataset) "Diagram dataset must not be null."

        let modules =
            match dataset.Modules with
            | null -> Array.empty
            | _ -> dataset.Modules |> Array.copy |> sanitizeModules

        if modules.Length = 0 then
            Array.empty<PagePlan>
        else
            let maxConnectors, occupancyLimit = normalizeOptions thresholds
            let plans = ResizeArray<PagePlan>()
            let mutable currentModules = ResizeArray<string>()
            let mutable currentConnectors = 0
            let mutable currentNodes = 0
            let mutable currentOccupancy = 0.0
            let mutable pageIndex = 0

            let flush () =
                if currentModules.Count > 0 then
                    plans.Add(
                        { PageIndex = pageIndex
                          Modules = currentModules.ToArray()
                          Connectors = currentConnectors
                          Nodes = currentNodes
                          Occupancy = currentOccupancy })
                    pageIndex <- pageIndex + 1
                    currentModules <- ResizeArray<string>()
                    currentConnectors <- 0
                    currentNodes <- 0
                    currentOccupancy <- 0.0

            for moduleStats in modules do
                let occupancyValue = moduleStats.OccupancyPercent
                let nextConnectors = currentConnectors + moduleStats.ConnectorCount
                let nextNodes = currentNodes + moduleStats.NodeCount
                let nextOccupancy =
                    if currentModules.Count = 0 then occupancyValue
                    else currentOccupancy + occupancyValue

                let connectorsOverflow =
                    currentModules.Count > 0 && nextConnectors > maxConnectors

                let occupancyOverflow =
                    currentModules.Count > 0 && nextOccupancy > occupancyLimit

                if connectorsOverflow || occupancyOverflow then
                    flush()

                currentModules.Add(moduleStats.ModuleId)
                currentConnectors <- currentConnectors + moduleStats.ConnectorCount
                currentNodes <- currentNodes + moduleStats.NodeCount
                currentOccupancy <-
                    if currentModules.Count = 1 then occupancyValue
                    else currentOccupancy + occupancyValue

            flush()
            plans.ToArray()

namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic

[<CLIMutable>]
type ModuleStats =
    { ModuleId: string
      ConnectorCount: int
      NodeCount: int
      OccupancyPercent: float
      HeightEstimate: float
      SpanMin: float
      SpanMax: float
      HasSpan: bool
      CrossModuleConnectors: int }

[<CLIMutable>]
type DiagramDataset =
    { Modules: ModuleStats array }

[<CLIMutable>]
type PageSplitOptions =
    { MaxConnectors: int
      MaxOccupancyPercent: float
      LaneSplitAllowed: bool
      MaxPageHeightIn: float
      MaxModulesPerPage: int
      HeightSlackPercent: float }

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
                let height =
                    if Double.IsNaN m.HeightEstimate || m.HeightEstimate < 0.0 then 0.0
                    else m.HeightEstimate
                let spanMin =
                    if Double.IsNaN m.SpanMin then 0.0 else m.SpanMin
                let spanMax =
                    if Double.IsNaN m.SpanMax then 0.0 else m.SpanMax
                let hasSpan =
                    m.HasSpan && not (Double.IsNaN m.SpanMin) && not (Double.IsNaN m.SpanMax)
                let spanMin, spanMax =
                    if hasSpan && spanMax < spanMin then spanMax, spanMin else spanMin, spanMax
                let cross =
                    if m.CrossModuleConnectors < 0 then 0 else m.CrossModuleConnectors
                { ModuleId = moduleId
                  ConnectorCount = connectors
                  NodeCount = nodeCount
                  OccupancyPercent = occupancy
                  HeightEstimate = height
                  SpanMin = spanMin
                  SpanMax = spanMax
                  HasSpan = hasSpan
                  CrossModuleConnectors = cross })

    let private normalizeOptions (thresholds: PageSplitOptions) =
        let maxConnectors =
            if thresholds.MaxConnectors <= 0 then Int32.MaxValue
            else thresholds.MaxConnectors

        let occupancyLimit =
            let raw = thresholds.MaxOccupancyPercent
            if thresholds.LaneSplitAllowed then Double.PositiveInfinity
            elif Double.IsNaN raw || raw <= 0.0 then Double.PositiveInfinity
            else raw

        let maxHeight =
            let raw = thresholds.MaxPageHeightIn
            if Double.IsNaN raw || raw <= 0.0 then Double.PositiveInfinity
            else raw

        let maxModules =
            if thresholds.MaxModulesPerPage <= 0 then Int32.MaxValue
            else thresholds.MaxModulesPerPage

        let slack =
            let raw = thresholds.HeightSlackPercent
            if Double.IsNaN raw || raw < 0.0 then 0.0
            else raw

        { thresholds with
            MaxConnectors = maxConnectors
            MaxOccupancyPercent = occupancyLimit
            MaxPageHeightIn = maxHeight
            MaxModulesPerPage = maxModules
            HeightSlackPercent = slack }

    let computePages (thresholds: PageSplitOptions) (dataset: DiagramDataset) =
        if isNull (box dataset) then invalidArg (nameof dataset) "Diagram dataset must not be null."

        let modules =
            match dataset.Modules with
            | null -> Array.empty
            | _ -> dataset.Modules |> Array.copy |> sanitizeModules

        if modules.Length = 0 then
            Array.empty<PagePlan>
        else
            let opts = normalizeOptions thresholds
            let plans = ResizeArray<PagePlan>()
            let mutable currentModules = ResizeArray<string>()
            let mutable currentConnectors = 0
            let mutable currentNodes = 0
            let mutable currentHasSpan = false
            let mutable currentSpanMin = 0.0
            let mutable currentSpanMax = 0.0
            let mutable currentLooseHeight = 0.0
            let mutable currentFallbackOccupancy = 0.0
            let mutable pageIndex = 0

            let connectorLimit = opts.MaxConnectors
            let moduleLimit = opts.MaxModulesPerPage
            let heightLimit =
                if Double.IsInfinity opts.MaxPageHeightIn then Double.PositiveInfinity
                else opts.MaxPageHeightIn * (1.0 + (opts.HeightSlackPercent / 100.0))

            let inline moduleHasSpan (m: ModuleStats) =
                m.HasSpan
                && not (Double.IsNaN(float m.SpanMin))
                && not (Double.IsNaN(float m.SpanMax))

            let inline moduleHeight (m: ModuleStats) =
                let h = float m.HeightEstimate
                if Double.IsNaN h || h <= 0.0 then
                    let fallback = float m.NodeCount
                    if fallback <= 0.0 then 1.0 else fallback
                else
                    h

            let computePageHeight hasSpan spanMin spanMax looseHeight =
                let spanHeight =
                    if hasSpan then
                        let diff = spanMax - spanMin
                        if diff <= 0.0 then 0.0 else diff
                    else
                        0.0
                if hasSpan && looseHeight > 0.0 then spanHeight + looseHeight
                elif hasSpan then spanHeight
                else looseHeight

            let flush () =
                if currentModules.Count > 0 then
                    let totalHeight = computePageHeight currentHasSpan currentSpanMin currentSpanMax currentLooseHeight
                    let occupancyRaw =
                        if Double.IsInfinity opts.MaxPageHeightIn then currentFallbackOccupancy
                        elif opts.MaxPageHeightIn <= 0.0 then 0.0
                        else (totalHeight / opts.MaxPageHeightIn) * 100.0
                    let occupancy =
                        if Double.IsNaN occupancyRaw then 0.0 else occupancyRaw
                    plans.Add(
                        { PageIndex = pageIndex
                          Modules = currentModules.ToArray()
                          Connectors = currentConnectors
                          Nodes = currentNodes
                          Occupancy = occupancy })
                    pageIndex <- pageIndex + 1
                    currentModules <- ResizeArray<string>()
                    currentConnectors <- 0
                    currentNodes <- 0
                    currentHasSpan <- false
                    currentSpanMin <- 0.0
                    currentSpanMax <- 0.0
                    currentLooseHeight <- 0.0
                    currentFallbackOccupancy <- 0.0

            for moduleStats in modules do
                let rec place () =
                    let moduleSpan = moduleHasSpan moduleStats

                    let newHasSpan, newSpanMin, newSpanMax =
                        if moduleSpan then
                            if currentHasSpan then
                                true,
                                min currentSpanMin (float moduleStats.SpanMin),
                                max currentSpanMax (float moduleStats.SpanMax)
                            else
                                true,
                                float moduleStats.SpanMin,
                                float moduleStats.SpanMax
                        else
                            currentHasSpan, currentSpanMin, currentSpanMax

                    let newLooseHeight =
                        if moduleSpan then currentLooseHeight
                        else currentLooseHeight + moduleHeight moduleStats

                    let newFallbackOccupancy =
                        if Double.IsInfinity opts.MaxPageHeightIn then
                            currentFallbackOccupancy + float moduleStats.OccupancyPercent
                        else
                            currentFallbackOccupancy

                    let newModulesCount = currentModules.Count + 1
                    let newConnectors = currentConnectors + moduleStats.ConnectorCount
                    let newNodes = currentNodes + moduleStats.NodeCount

                    let totalHeight =
                        let height = computePageHeight newHasSpan newSpanMin newSpanMax newLooseHeight
                        if height <= 0.0 then moduleHeight moduleStats else height

                    let occupancyFromHeight =
                        if Double.IsInfinity opts.MaxPageHeightIn || opts.MaxPageHeightIn <= 0.0 then 0.0
                        else (totalHeight / opts.MaxPageHeightIn) * 100.0

                    let connectorsOver = newConnectors > connectorLimit
                    let modulesOver = newModulesCount > moduleLimit
                    let heightOver =
                        if Double.IsInfinity heightLimit then false
                        else totalHeight > heightLimit

                    let occupancyOver =
                        if Double.IsInfinity opts.MaxPageHeightIn then
                            (not opts.LaneSplitAllowed)
                            && (not (Double.IsInfinity opts.MaxOccupancyPercent))
                            && (newFallbackOccupancy > opts.MaxOccupancyPercent)
                        else
                            false

                    let shouldFlush =
                        currentModules.Count > 0
                        && (connectorsOver || modulesOver || heightOver || occupancyOver)

                    if shouldFlush then
                        flush()
                        place()
                    else
                        currentModules.Add(moduleStats.ModuleId)
                        currentConnectors <- newConnectors
                        currentNodes <- newNodes
                        currentHasSpan <- newHasSpan
                        currentSpanMin <- newSpanMin
                        currentSpanMax <- newSpanMax
                        currentLooseHeight <- newLooseHeight
                        currentFallbackOccupancy <- newFallbackOccupancy
                place()

            flush()
            plans.ToArray()

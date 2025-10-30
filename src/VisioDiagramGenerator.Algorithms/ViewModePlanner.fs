namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic
open System.Globalization
open System.Text.Json
open System.Text.Json.Serialization
open VDG.Core.Models

[<AllowNullLiteral>]
type private ContainerMetadata() =
    [<JsonPropertyName("id")>]
    member val Id: string = null with get, set

    [<JsonPropertyName("tier")>]
    member val Tier: string = null with get, set

type private ModuleEdgeStats =
    { mutable ConnectorCount: int
      mutable CrossModuleCount: int }

module ViewModePlanner =

    let private jsonOptions =
        JsonSerializerOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)

    type private ViewModule =
        { Id: string
          Tier: string
          Nodes: ResizeArray<Node> }

    type private SegmentMetrics =
        { Columns: int
          CardWidth: float32
          CardHeight: float32
          VisibleCount: int
          Overflow: int }

    let private defaultTiers =
        [| "External"; "Edge"; "Services"; "Data"; "Observability"; "Modules" |]

    let private getOrderedTiers (model: DiagramModel) =
        match model.Metadata.TryGetValue("layout.tiers") with
        | true, value when not (String.IsNullOrWhiteSpace value) ->
            value.Split([| ','; '|'; ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (fun s -> s.Length > 0)
            |> fun tiers -> if tiers.Length = 0 then defaultTiers else tiers
        | _ -> defaultTiers

    let private tryGetContainerTierMap (model: DiagramModel) =
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        match model.Metadata.TryGetValue("layout.containers.json") with
        | true, json when not (String.IsNullOrWhiteSpace json) ->
            try
                let parsed = JsonSerializer.Deserialize<ContainerMetadata array>(json, jsonOptions)
                if not (isNull parsed) then
                    for entry in parsed do
                        if not (isNull entry) && not (String.IsNullOrWhiteSpace entry.Id) then
                            let tier =
                                if String.IsNullOrWhiteSpace entry.Tier then null
                                else entry.Tier.Trim()
                            map[entry.Id.Trim()] <- tier
            with _ -> ()
        | _ -> ()
        map

    let private getMetadataDouble (model: DiagramModel) (key: string) =
        match model.Metadata.TryGetValue(key) with
        | true, value when not (String.IsNullOrWhiteSpace value) ->
            match Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | _ -> None
        | _ -> None

    let private getMetadataInt (model: DiagramModel) (key: string) =
        match model.Metadata.TryGetValue(key) with
        | true, value when not (String.IsNullOrWhiteSpace value) ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | _ -> None
        | _ -> None

    let private getMetadataBool (model: DiagramModel) (key: string) =
        match model.Metadata.TryGetValue(key) with
        | true, value when not (String.IsNullOrWhiteSpace value) ->
            match Boolean.TryParse(value) with
            | true, parsed -> Some parsed
            | _ -> None
        | _ -> None

    let private hasTitle (model: DiagramModel) =
        match model.Metadata.TryGetValue("title") with
        | true, value when not (String.IsNullOrWhiteSpace value) -> true
        | _ -> false

    let private computeUsableHeight (model: DiagramModel) (canvasHeight: float32) =
        let margin = getMetadataDouble model "layout.page.marginIn" |> Option.defaultValue 1.0
        let titleHeight = if hasTitle model then 0.6 else 0.0
        let pageHeight =
            match getMetadataDouble model "layout.page.heightIn" with
            | Some v when v > 0.0 -> Some v
            | _ ->
                let fallback = float canvasHeight
                if fallback > 0.0 then Some fallback else None
        match pageHeight with
        | Some height ->
            let usable = height - (2.0 * margin) - titleHeight
            if usable > 0.01 then Some usable else None
        | None -> None

    let private buildPageSplitOptions (model: DiagramModel) (usableHeight: float option) =
        let mutable options =
            { MaxConnectors = 400
              MaxOccupancyPercent = 110.0
              LaneSplitAllowed = false
              MaxPageHeightIn = 0.0
              MaxModulesPerPage = 10
              HeightSlackPercent = 25.0 }

        let isViewMode =
            match model.Metadata.TryGetValue("layout.outputMode") with
            | true, value when not (String.IsNullOrWhiteSpace value) ->
                String.Equals(value.Trim(), "view", StringComparison.OrdinalIgnoreCase)
            | _ -> false

        if isViewMode then
            options <- { options with
                            MaxOccupancyPercent = 95.0
                            MaxModulesPerPage = 6
                            HeightSlackPercent = 12.5 }

        match getMetadataInt model "layout.page.plan.maxConnectors" with
        | Some value when value > 0 ->
            options <- { options with MaxConnectors = value }
        | _ -> ()

        match getMetadataDouble model "layout.page.plan.maxOccupancyPercent" with
        | Some value when value > 0.0 ->
            options <- { options with MaxOccupancyPercent = value }
        | _ -> ()

        match getMetadataBool model "layout.page.plan.laneSplitAllowed" with
        | Some flag -> options <- { options with LaneSplitAllowed = flag }
        | _ -> ()

        match usableHeight with
        | Some value when value > 0.01 ->
            options <- { options with MaxPageHeightIn = value }
        | _ -> ()

        match getMetadataDouble model "layout.page.plan.maxHeightIn" with
        | Some value when value > 0.0 ->
            options <- { options with MaxPageHeightIn = value }
        | _ -> ()

        match getMetadataInt model "layout.page.plan.maxModulesPerPage" with
        | Some value when value > 0 ->
            options <- { options with MaxModulesPerPage = value }
        | _ -> ()

        match getMetadataDouble model "layout.page.plan.heightSlackPercent" with
        | Some value when value >= 0.0 ->
            options <- { options with HeightSlackPercent = value }
        | _ -> ()

        options

    let private clampLayerBudget defaultValue value =
        match value with
        | Some raw when raw < 1 -> 1
        | Some raw when raw > 1000 -> 1000
        | Some raw -> raw
        | None -> defaultValue

    let private buildLayerSplitOptions (model: DiagramModel) =
        let maxShapes = getMetadataInt model "layout.layers.maxShapes" |> clampLayerBudget 900
        let maxConnectors = getMetadataInt model "layout.layers.maxConnectors" |> clampLayerBudget 900
        { MaxShapes = maxShapes
          MaxConnectors = maxConnectors }

    let private getModuleEdgeStats (stats: Dictionary<string, ModuleEdgeStats>) (moduleId: string) =
        match stats.TryGetValue moduleId with
        | true, existing -> existing
        | _ ->
            let created = { ConnectorCount = 0; CrossModuleCount = 0 }
            stats[moduleId] <- created
            created

    let private tryGetMetadata (node: Node) (key: string) =
        if node.Metadata = null then None
        else
            match node.Metadata.TryGetValue key with
            | true, value when not (String.IsNullOrWhiteSpace value) -> Some (value.Trim())
            | _ -> None

    let private resolveTier (tiers: string array) (tiersSet: HashSet<string>) (node: Node) =
        if not (isNull node.Tier) && tiersSet.Contains(node.Tier.Trim()) then
            node.Tier.Trim()
        else
            match tryGetMetadata node "tier" with
            | Some tier when tiersSet.Contains tier -> tier
            | _ -> tiers[0]

    let private resolveModuleId (node: Node) (tiers: string array) (tiersSet: HashSet<string>) =
        let fallback = tiers[0]
        if not (String.IsNullOrWhiteSpace node.GroupId) then
            node.GroupId.Trim()
        else
            match tryGetMetadata node "moduleId" with
            | Some moduleId -> moduleId
            | None ->
                match tryGetMetadata node "node.containerId" with
                | Some containerId -> containerId
                | None ->
                    if not (String.IsNullOrWhiteSpace node.Tier) && tiersSet.Contains(node.Tier.Trim()) then
                        node.Tier.Trim()
                    else
                        match tryGetMetadata node "tier" with
                        | Some tier when tiersSet.Contains tier -> tier
                        | _ -> fallback

    let inline private fmin (a: float32) (b: float32) = if a <= b then a else b
    let inline private fmax (a: float32) (b: float32) = if a >= b then a else b

    let private point (x: float32) (y: float32) = { X = x; Y = y }

    let private midpoint (a: PointF) (b: PointF) =
        point ((a.X + b.X) / 2.f) ((a.Y + b.Y) / 2.f)

    let private computeAnchor (fromRect: RectangleF) (toRect: RectangleF) =
        let fromCenterX = fromRect.Left + (fromRect.Width / 2.f)
        let fromCenterY = fromRect.Bottom + (fromRect.Height / 2.f)
        let toCenterX = toRect.Left + (toRect.Width / 2.f)
        let toCenterY = toRect.Bottom + (toRect.Height / 2.f)
        let dx = toCenterX - fromCenterX
        let dy = toCenterY - fromCenterY
        if Math.Abs dx >= Math.Abs dy then
            let x = if dx >= 0.f then fromRect.Left + fromRect.Width else fromRect.Left
            point x fromCenterY
        else
            let y = if dy >= 0.f then fromRect.Bottom + fromRect.Height else fromRect.Bottom
            point fromCenterX y

    [<CompiledName("ComputeViewLayout")>]
    let computeViewLayout (model: DiagramModel) : LayoutPlan =
        if isNull model then nullArg "model"

        let tiers = getOrderedTiers model
        let tiersSet = HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase)
        let containerTierMap = tryGetContainerTierMap model

        let baseModules = Dictionary<string, ViewModule>(StringComparer.OrdinalIgnoreCase)
        let moduleOrder = ResizeArray<string>()
        let moduleEdgeStats = Dictionary<string, ModuleEdgeStats>(StringComparer.OrdinalIgnoreCase)
        for node in model.Nodes do
            if not (isNull node) then
                let moduleId = resolveModuleId node tiers tiersSet
                let viewModule =
                    match baseModules.TryGetValue moduleId with
                    | true, m -> m
                    | _ ->
                        let tier =
                            match containerTierMap.TryGetValue moduleId with
                            | true, mapped when not (String.IsNullOrWhiteSpace mapped) && tiersSet.Contains(mapped) -> mapped
                            | _ -> resolveTier tiers tiersSet node
                        let created = { Id = moduleId; Tier = tier; Nodes = ResizeArray() }
                        baseModules[moduleId] <- created
                        moduleOrder.Add moduleId
                        created
                viewModule.Nodes.Add node

        if baseModules.Count = 0 then
            { OutputMode = "view"
              CanvasWidth = 0.f
              CanvasHeight = 0.f
              PageHeight = 0.f
              PageMargin = 0.f
              TitleHeight = 0.f
              Nodes = Array.empty
              NodeModules = Array.empty
              PageLayouts = Array.empty
              Containers = Array.empty
              Edges = Array.empty
              Pages = Array.empty
              Layers = Array.empty
              Bridges = Array.empty
              PageBridges = Array.empty
              Stats =
                { NodeCount = 0
                  ConnectorCount = 0
                  ModuleCount = 0
                  ContainerCount = 0
                  ModuleIds = Array.empty
                  Overflows = Array.empty } }
        else
            let slotsPerTier = 6
            let slotsPerRow = 3
            let tierSpacing = 1.4f
            let rowSpacing = 0.85f
            let cardSpacingX = 0.75f
            let cardSpacingY = 0.8f
            let cardPadding = 0.3f
            let baseNodeWidth = 1.35f
            let minNodeChars = 16
            let maxNodeChars = 64
            let charWidth = 0.075f
            let maxNodeWidth = 3.75f
            let nodeHeight = 0.34f
            let columnGap = 0.18f
            let rowGap = 0.08f
            let headerHeight = 0.55f
            let maxRowsPerColumn = 6
            let maxColumns = 3
            let maxCardHeight = 4.0f

            let computeLongestLabelStats (nodes: seq<Node>) =
                let mutable longestLabel = 0
                let mutable longestToken = 0
                let tokenSeparators =
                    [| ' '; '\t'; '\r'; '\n'; '.'; ','; ';'; ':'; '-'; '_'; '/'; '\\' |]

                for node in nodes do
                    if not (isNull node) && not (String.IsNullOrWhiteSpace node.Label) then
                        let label = node.Label.Trim()
                        let length = label.Length
                        if length > longestLabel then
                            longestLabel <- length

                        let tokens =
                            label.Split(tokenSeparators, StringSplitOptions.RemoveEmptyEntries)
                        for token in tokens do
                            let tokenLen = token.Length
                            if tokenLen > longestToken then
                                longestToken <- tokenLen

                longestLabel, longestToken

            let globalLabelStats =
                if isNull (box model.Nodes) || model.Nodes.Count = 0 then
                    0, 0
                else
                    computeLongestLabelStats model.Nodes

            let nodeWidth =
                let longestLabel, longestToken = globalLabelStats
                let biasedTokenLen =
                    if longestToken <= 0 then 0
                    else Math.Ceiling(float longestToken * 1.25) |> int
                let candidateChars =
                    [| longestLabel; biasedTokenLen; minNodeChars |]
                    |> Array.max
                    |> min maxNodeChars

                let width =
                    baseNodeWidth + float32 (candidateChars - minNodeChars) * charWidth
                width |> max baseNodeWidth |> min maxNodeWidth

            let preLayoutUsableHeight = computeUsableHeight model 0.f
            let maxRowsByHeight =
                match preLayoutUsableHeight with
                | Some usable ->
                    let available = usable - (2.0 * float cardPadding + float headerHeight + float rowGap)
                    if available <= 0.0 then 1
                    else
                        let perRow = float nodeHeight + float rowGap
                        Math.Floor((available + float rowGap) / perRow) |> int |> max 1 |> min maxRowsPerColumn
                | None -> maxRowsPerColumn

            let maxRowsByHeight = Math.Max(1, maxRowsByHeight)
            let segmentCapacity = Math.Max(1, maxRowsByHeight * 2)

            let computeSegmentMetrics totalNodes =
                let safeTotal = if totalNodes <= 0 then 0 else totalNodes
                if safeTotal = 0 then
                    { Columns = 1
                      CardWidth = cardPadding * 2.f + nodeWidth
                      CardHeight = cardPadding + headerHeight + rowGap + cardPadding
                      VisibleCount = 0
                      Overflow = 0 }
                else
                    let mutable visible = Math.Min(safeTotal, segmentCapacity)
                    let requiredColumns =
                        int (Math.Ceiling(float visible / float maxRowsByHeight))
                    let mutable columns = Math.Min(maxColumns, Math.Max(1, requiredColumns))
                    let mutable rows =
                        Math.Max(1, int (Math.Ceiling(float visible / float columns)))
                    rows <- Math.Min(maxRowsByHeight, rows)

                    let horizontalGap = if columns > 1 then float32 (columns - 1) * columnGap else 0.f
                    let cardWidth = cardPadding * 2.f + (float32 columns * nodeWidth) + horizontalGap
                    let verticalGap = if rows > 1 then float32 (rows - 1) * rowGap else 0.f
                    let listHeight = float32 rows * nodeHeight + verticalGap
                    let mutable cardHeight = cardPadding + headerHeight + rowGap + listHeight + cardPadding
                    let mutable overflow = safeTotal - visible

                    if cardHeight > maxCardHeight then
                        let usableList = maxCardHeight - (cardPadding + headerHeight + rowGap + cardPadding)
                        let rowsThatFit =
                            Math.Max(
                                1,
                                int (
                                    Math.Floor(
                                        (float usableList + float rowGap)
                                        / (float nodeHeight + float rowGap))))
                        visible <- Math.Min(safeTotal, rowsThatFit * columns)
                        overflow <- safeTotal - visible
                        cardHeight <- maxCardHeight

                    let columns = if columns <= 0 then 1 else columns
                    { Columns = columns
                      CardWidth = cardWidth
                      CardHeight = cardHeight
                      VisibleCount = visible
                      Overflow = overflow }

            let modules = Dictionary<string, ViewModule>(StringComparer.OrdinalIgnoreCase)
            let segmentOrigins = Dictionary<string, (string * int * int)>(StringComparer.OrdinalIgnoreCase)
            let nodeToSegment = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            let segmentMetrics = Dictionary<string, SegmentMetrics>(StringComparer.OrdinalIgnoreCase)
            let orderedSegments = ResizeArray<string>()
            let moduleToPage = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)

            let nodeLookup = Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase)
            for node in model.Nodes do
                if not (isNull node) && not (String.IsNullOrWhiteSpace node.Id) then
                    nodeLookup[node.Id] <- node

            for moduleId in moduleOrder do
                let baseModule = baseModules[moduleId]
                let orderedNodes =
                    baseModule.Nodes
                    |> Seq.filter (fun n -> not (isNull n))
                    |> Seq.sortWith (fun a b ->
                        let primary = StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label)
                        if primary <> 0 then primary
                        else StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id))
                    |> Seq.toArray

                let chunks =
                    if orderedNodes.Length = 0 then
                        [| Array.empty<Node> |]
                    else
                        orderedNodes |> Array.chunkBySize segmentCapacity

                let segmentCount = chunks.Length
                for idxSeg = 0 to segmentCount - 1 do
                    let chunk = chunks[idxSeg]
                    let segmentId =
                        if segmentCount = 1 then moduleId
                        else sprintf "%s#part%d" moduleId (idxSeg + 1)

                    let segmentModule = { Id = segmentId; Tier = baseModule.Tier; Nodes = ResizeArray() }
                    for node in chunk do
                        segmentModule.Nodes.Add node
                        nodeToSegment[node.Id] <- segmentId

                    modules[segmentId] <- segmentModule
                    segmentOrigins[segmentId] <- (moduleId, idxSeg, segmentCount)
                    orderedSegments.Add segmentId

                    let metrics = computeSegmentMetrics segmentModule.Nodes.Count
                    segmentMetrics[segmentId] <- metrics

            let segmentEdgeStats = Dictionary<string, ModuleEdgeStats>(StringComparer.OrdinalIgnoreCase)
            if not (isNull model.Edges) then
                for edge in model.Edges do
                    if not (isNull edge)
                       && not (String.IsNullOrWhiteSpace edge.SourceId)
                       && not (String.IsNullOrWhiteSpace edge.TargetId) then
                        let resolveSegment nodeId =
                            match nodeToSegment.TryGetValue nodeId with
                            | true, segId -> segId
                            | _ ->
                                match nodeLookup.TryGetValue nodeId with
                                | true, node -> resolveModuleId node tiers tiersSet
                                | _ -> String.Empty

                        let srcModuleId = resolveSegment edge.SourceId
                        let dstModuleId = resolveSegment edge.TargetId

                        if not (String.IsNullOrWhiteSpace srcModuleId)
                           && not (String.IsNullOrWhiteSpace dstModuleId) then
                            let isCross = not (srcModuleId.Equals(dstModuleId, StringComparison.OrdinalIgnoreCase))
                            let recordEdgeStats moduleId =
                                if not (String.IsNullOrWhiteSpace moduleId) then
                                    let stats = getModuleEdgeStats segmentEdgeStats moduleId
                                    stats.ConnectorCount <- stats.ConnectorCount + 1
                                    if isCross then stats.CrossModuleCount <- stats.CrossModuleCount + 1
                            recordEdgeStats srcModuleId
                            recordEdgeStats dstModuleId

            let buildModuleStatsForSegment segmentId =
                match modules.TryGetValue segmentId with
                | true, segmentModule ->
                    let metrics =
                        match segmentMetrics.TryGetValue segmentId with
                        | true, value -> value
                        | _ ->
                            let computed = computeSegmentMetrics segmentModule.Nodes.Count
                            segmentMetrics[segmentId] <- computed
                            computed

                    let edgeStats =
                        match segmentEdgeStats.TryGetValue segmentId with
                        | true, stats -> stats
                        | _ -> { ConnectorCount = 0; CrossModuleCount = 0 }

                    let height = float metrics.CardHeight
                    let occupancy =
                        match preLayoutUsableHeight with
                        | Some usable when usable > 0.0 ->
                            let percent = (height / usable) * 100.0
                            Math.Min(100.0, Math.Max(0.0, percent))
                        | _ -> 0.0

                    Some
                        { ModuleId = segmentId
                          ConnectorCount = edgeStats.ConnectorCount
                          NodeCount = segmentModule.Nodes.Count
                          OccupancyPercent = occupancy
                          HeightEstimate = if height > 0.0 then height else float nodeHeight
                          SpanMin = 0.0
                          SpanMax = height
                          HasSpan = false
                          CrossModuleConnectors = edgeStats.CrossModuleCount }
                | _ -> None

            let moduleStatsForPlan =
                orderedSegments
                |> Seq.toArray
                |> Array.choose buildModuleStatsForSegment

            let datasetForPlan = { Modules = moduleStatsForPlan }
            let pageOptionsPre = buildPageSplitOptions model preLayoutUsableHeight

            let initialPagePlans =
                if moduleStatsForPlan.Length = 0 then
                    Array.empty
                else
                    PagingPlanner.computePages pageOptionsPre datasetForPlan

            let plannedPagePlans =
                if initialPagePlans.Length = 0 && orderedSegments.Count > 0 then
                    [| { PageIndex = 0
                         Modules = orderedSegments.ToArray()
                         Connectors = 0
                         Nodes = 0
                         Occupancy = 0.0 } |]
                else
                    initialPagePlans

            for plan in plannedPagePlans do
                if not (isNull (box plan)) && not (isNull plan.Modules) then
                    for moduleId in plan.Modules do
                        if not (String.IsNullOrWhiteSpace moduleId) then
                            let trimmed = moduleId.Trim()
                            moduleToPage[trimmed] <- plan.PageIndex

            let segmentsByPageTier = Dictionary<int, Dictionary<string, ResizeArray<string>>>()

            let getPageTierBucket pageIndex (tier: string) =
                let tiersForPage =
                    match segmentsByPageTier.TryGetValue pageIndex with
                    | true, existing -> existing
                    | _ ->
                        let created = Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)
                        segmentsByPageTier[pageIndex] <- created
                        created

                match tiersForPage.TryGetValue tier with
                | true, bucket -> bucket
                | _ ->
                    let created = ResizeArray<string>()
                    tiersForPage[tier] <- created
                    created

            for segmentId in orderedSegments do
                let pageIndex =
                    match moduleToPage.TryGetValue segmentId with
                    | true, value -> value
                    | _ ->
                        moduleToPage[segmentId] <- 0
                        0
                match modules.TryGetValue segmentId with
                | true, segmentModule ->
                    let bucket = getPageTierBucket pageIndex segmentModule.Tier
                    bucket.Add segmentId
                | _ -> ()

            moduleEdgeStats.Clear()

            let nodeLayouts = ResizeArray<NodeLayout>()
            let placementCenters = Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase)
            let containerLayouts = ResizeArray<ContainerLayout>()
            let truncated = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            let pageExtents = Dictionary<int, float32[]>(HashIdentity.Structural)

            let getPageExtents index =
                match pageExtents.TryGetValue index with
                | true, bounds -> bounds
                | _ ->
                    let created =
                        [| Single.PositiveInfinity
                           Single.PositiveInfinity
                           Single.NegativeInfinity
                           Single.NegativeInfinity |]
                    pageExtents[index] <- created
                    created

            let updatePageExtents index left bottom width height =
                let bounds = getPageExtents index
                if left < bounds[0] then bounds[0] <- left
                if bottom < bounds[1] then bounds[1] <- bottom
                let right = left + width
                let top = bottom + height
                if right > bounds[2] then bounds[2] <- right
                if top > bounds[3] then bounds[3] <- top

            let pageSpacing = tierSpacing * 3.f

            let defaultPageMargin = 1.0
            let margin = float32 (getMetadataDouble model "layout.page.marginIn" |> Option.defaultValue defaultPageMargin)
            let titleHeight = float32 (if hasTitle model then 0.6 else 0.0)
            let pageHeight =
                match getMetadataDouble model "layout.page.heightIn" with
                | Some value when value > 0.0 -> float32 value
                | _ -> 0.f
            let pageBodyHeight =
                if pageHeight > 0.f then
                    pageHeight - (2.f * margin) - titleHeight
                else
                    0.f

            let mutable cursorY = 0.f
            let mutable minLeft = Single.PositiveInfinity
            let mutable maxRight = Single.NegativeInfinity
            let mutable minBottom = Single.PositiveInfinity
            let mutable maxTop = Single.NegativeInfinity

            let updateExtents left bottom width height =
                minLeft <- fmin minLeft left
                minBottom <- fmin minBottom bottom
                maxRight <- fmax maxRight (left + width)
                maxTop <- fmax maxTop (bottom + height)

            let orderedPagePlans =
                if plannedPagePlans.Length = 0 then
                    [| { PageIndex = 0
                         Modules = orderedSegments.ToArray()
                         Connectors = 0
                         Nodes = 0
                         Occupancy = 0.0 } |]
                else
                    plannedPagePlans |> Array.sortBy (fun plan -> plan.PageIndex)

            for plan in orderedPagePlans do
                let pageIndex = plan.PageIndex
                if pageIndex > 0 then
                    cursorY <- 0.f
                let tierMap =
                    match segmentsByPageTier.TryGetValue pageIndex with
                    | true, value -> value
                    | _ -> Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)

                for tier in tiers do
                    let modulesInTier =
                        match tierMap.TryGetValue tier with
                        | true, bucket when bucket.Count > 0 -> bucket |> Seq.toArray
                        | _ -> Array.empty

                    if modulesInTier.Length = 0 then
                        cursorY <- cursorY - tierSpacing
                    else
                        let mutable slotIndex = 0
                        for segmentId in modulesInTier do
                            match modules.TryGetValue segmentId with
                            | true, viewModule ->
                                let orderedNodes =
                                    viewModule.Nodes
                                    |> Seq.filter (fun n -> not (isNull n))
                                    |> Seq.toArray

                                if orderedNodes.Length = 0 then
                                    slotIndex <- slotIndex + 1
                                else
                                    let metrics =
                                        match segmentMetrics.TryGetValue segmentId with
                                        | true, value -> value
                                        | _ ->
                                            let computed = computeSegmentMetrics orderedNodes.Length
                                            segmentMetrics[segmentId] <- computed
                                            computed

                                    let columns = Math.Max(1, metrics.Columns)
                                    let cardWidth = metrics.CardWidth
                                    let cardHeight = metrics.CardHeight
                                    let visibleCount = Math.Min(metrics.VisibleCount, orderedNodes.Length)
                                    let overflow = Math.Max(0, orderedNodes.Length - visibleCount)

                                    let slotRow = slotIndex / slotsPerRow
                                    let slotCol = slotIndex % slotsPerRow
                                    let left = float32 slotCol * (cardWidth + cardSpacingX)
                                    let bottom = cursorY - float32 slotRow * (cardHeight + cardSpacingY)
                                    let bodyTop = bottom + cardHeight - headerHeight - rowGap
                                    updatePageExtents pageIndex left bottom cardWidth cardHeight

                                    for idx = 0 to visibleCount - 1 do
                                        let node = orderedNodes[idx]
                                        let col = idx % columns
                                        let rowIndex = idx / columns
                                        let nodeLeft = left + cardPadding + float32 col * (nodeWidth + columnGap)
                                        let nodeTop = bodyTop - float32 rowIndex * (nodeHeight + rowGap)
                                        let nodeBottom = nodeTop - nodeHeight
                                        let layout =
                                            { Id = node.Id
                                              Position = point nodeLeft nodeBottom
                                              Size = Nullable<Size>(Size(nodeWidth, nodeHeight)) }
                                        nodeLayouts.Add layout
                                        placementCenters[node.Id] <- point (nodeLeft + (nodeWidth / 2.f)) (nodeBottom + (nodeHeight / 2.f))
                                        updatePageExtents pageIndex nodeLeft nodeBottom nodeWidth nodeHeight

                                    if overflow > 0 then
                                        let baseId =
                                            match segmentOrigins.TryGetValue segmentId with
                                            | true, (originalId, _, _) -> originalId
                                            | _ -> segmentId
                                        let existing =
                                            match truncated.TryGetValue baseId with
                                            | true, value -> value
                                            | _ -> 0
                                        truncated[baseId] <- existing + overflow
                                        let badgeText = sprintf "+%d." overflow
                                        let badgeWidth = fmax 0.9f (float32 badgeText.Length * 0.1f)
                                        let badgeLeft = left + cardWidth - cardPadding - badgeWidth
                                        let badgeBottom = bottom + cardPadding * 0.5f
                                        let badgeLayout =
                                            { Id = $"{segmentId}#overflow"
                                              Position = point badgeLeft badgeBottom
                                              Size = Nullable<Size>(Size(badgeWidth, 0.3f)) }
                                        nodeLayouts.Add badgeLayout

                                    let baseLabel =
                                        match segmentOrigins.TryGetValue segmentId with
                                        | true, (originalId, index, count) when count > 1 -> sprintf "%s (part %d/%d)" originalId (index + 1) count
                                        | true, (originalId, _, _) -> originalId
                                        | _ -> segmentId
                                    let label =
                                        if overflow > 0 then
                                            sprintf "%s (+%d)" baseLabel overflow
                                        else
                                            baseLabel

                                    containerLayouts.Add(
                                        { Id = segmentId
                                          Label = label
                                          Tier = viewModule.Tier
                                          Bounds =
                                            { Left = left
                                              Bottom = bottom
                                              Width = cardWidth
                                              Height = cardHeight }
                                          VisibleNodes = visibleCount
                                          OverflowCount = overflow })

                                    updateExtents left bottom cardWidth cardHeight

                                    slotIndex <- slotIndex + 1
                                    if slotIndex >= slotsPerTier then
                                        cursorY <- bottom - cardHeight - tierSpacing
                                        slotIndex <- 0
                            | _ -> slotIndex <- slotIndex + 1

                        if slotIndex > 0 then
                            let usedRows = int (Math.Ceiling(float slotIndex / float slotsPerRow))
                            cursorY <- cursorY - (float32 usedRows * (headerHeight + cardPadding + rowSpacing))

                        cursorY <- cursorY - tierSpacing

                if pageBodyHeight > 0.f then
                    let excess = cursorY - (-pageBodyHeight)
                    if excess < 0.f then
                        cursorY <- cursorY - pageSpacing
                    else
                        cursorY <- cursorY - pageSpacing - excess
                else
                    cursorY <- cursorY - pageSpacing

            let moduleBounds = Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase)
            for container in containerLayouts do
                if not (String.IsNullOrWhiteSpace container.Id) then
                    moduleBounds[container.Id] <- container.Bounds

            let nodeLookup = Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase)
            for node in model.Nodes do
                if not (isNull node) && not (String.IsNullOrWhiteSpace node.Id) then
                    nodeLookup[node.Id] <- node

            let edgeRoutes = ResizeArray<EdgeRoute>()
            for edge in model.Edges do
                if not (isNull edge) && not (String.IsNullOrWhiteSpace edge.SourceId) && not (String.IsNullOrWhiteSpace edge.TargetId) then
                    match placementCenters.TryGetValue edge.SourceId, placementCenters.TryGetValue edge.TargetId with
                    | (true, srcCenter), (true, dstCenter) when nodeLookup.ContainsKey edge.SourceId && nodeLookup.ContainsKey edge.TargetId ->
                        let srcNode = nodeLookup[edge.SourceId]
                        let dstNode = nodeLookup[edge.TargetId]
                        let srcModuleId =
                            match nodeToSegment.TryGetValue edge.SourceId with
                            | true, segId -> segId
                            | _ -> resolveModuleId srcNode tiers tiersSet
                        let dstModuleId =
                            match nodeToSegment.TryGetValue edge.TargetId with
                            | true, segId -> segId
                            | _ -> resolveModuleId dstNode tiers tiersSet
                        let points =
                            if srcModuleId.Equals(dstModuleId, StringComparison.OrdinalIgnoreCase)
                               || not (moduleBounds.ContainsKey srcModuleId)
                               || not (moduleBounds.ContainsKey dstModuleId) then
                                [| srcCenter; dstCenter |]
                            else
                                let exitPt = computeAnchor moduleBounds[srcModuleId] moduleBounds[dstModuleId]
                                let entryPt = computeAnchor moduleBounds[dstModuleId] moduleBounds[srcModuleId]
                                let mid = midpoint exitPt entryPt
                                [| srcCenter; exitPt; mid; entryPt; dstCenter |]

                        let isCrossModule = not (srcModuleId.Equals(dstModuleId, StringComparison.OrdinalIgnoreCase))
                        let recordEdge moduleId =
                            if not (String.IsNullOrWhiteSpace moduleId) then
                                let stats = getModuleEdgeStats moduleEdgeStats moduleId
                                stats.ConnectorCount <- stats.ConnectorCount + 1
                                if isCrossModule then stats.CrossModuleCount <- stats.CrossModuleCount + 1

                        recordEdge srcModuleId
                        recordEdge dstModuleId
                        edgeRoutes.Add(
                            { Id = if String.IsNullOrWhiteSpace edge.Id then $"{edge.SourceId}->{edge.TargetId}" else edge.Id
                              Points = points })
                    | _ -> ()

            let nodesArray = nodeLayouts.ToArray()
            let containersArray = containerLayouts.ToArray()
            let edgesArray = edgeRoutes.ToArray()

            let nodeModules =
                let assignments = ResizeArray<NodeModuleAssignment>(nodeLookup.Count)
                for node in model.Nodes do
                    if not (isNull node) && not (String.IsNullOrWhiteSpace node.Id) then
                        let moduleId =
                            match nodeToSegment.TryGetValue node.Id with
                            | true, segId when not (String.IsNullOrWhiteSpace segId) -> segId
                            | _ -> resolveModuleId node tiers tiersSet
                        if not (String.IsNullOrWhiteSpace moduleId) then
                            assignments.Add(
                                { NodeId = node.Id
                                  ModuleId = moduleId })
                assignments.ToArray()

            let canvasWidth =
                if nodesArray.Length = 0 then 0.f else fmax 0.f (maxRight - minLeft)

            let canvasHeight =
                if nodesArray.Length = 0 then 0.f else fmax 0.f (maxTop - minBottom)

            let usableHeight = computeUsableHeight model canvasHeight

            let moduleStats =
                containersArray
                |> Array.choose (fun container ->
                    if String.IsNullOrWhiteSpace container.Id then
                        None
                    elif not (modules.ContainsKey container.Id) then
                        None
                    else
                        let viewModule = modules[container.Id]
                        let edgeStats =
                            match moduleEdgeStats.TryGetValue container.Id with
                            | true, stats -> stats
                            | _ -> { ConnectorCount = 0; CrossModuleCount = 0 }
                        let height = float container.Bounds.Height
                        let occupancy =
                            match usableHeight with
                            | Some usable when usable > 0.0 ->
                                Math.Min(100.0, Math.Max(0.0, (height / usable) * 100.0))
                            | _ -> 0.0
                        Some
                            { ModuleId = container.Id
                              ConnectorCount = edgeStats.ConnectorCount
                              NodeCount = viewModule.Nodes.Count
                              OccupancyPercent = occupancy
                              HeightEstimate = if height > 0.0 then height else float nodeHeight
                              SpanMin = float container.Bounds.Bottom
                              SpanMax = float (container.Bounds.Bottom + container.Bounds.Height)
                              HasSpan = true
                              CrossModuleConnectors = edgeStats.CrossModuleCount })

            let dataset = { Modules = moduleStats }

            let pagePlans =
                if moduleStats.Length = 0 then
                    Array.empty
                else
                    plannedPagePlans

            moduleToPage.Clear()
            for plan in pagePlans do
                if not (isNull (box plan)) && not (isNull plan.Modules) then
                    for moduleId in plan.Modules do
                        if not (String.IsNullOrWhiteSpace moduleId) then
                            let trimmed = moduleId.Trim()
                            if not (moduleToPage.ContainsKey trimmed) then
                                moduleToPage[trimmed] <- plan.PageIndex
                            match segmentOrigins.TryGetValue trimmed with
                            | true, (originalId, _, _) when not (String.IsNullOrWhiteSpace originalId) ->
                                let baseId = originalId.Trim()
                                if not (moduleToPage.ContainsKey baseId) then
                                    moduleToPage[baseId] <- plan.PageIndex
                            | _ -> ()

            let pageBridges = ResizeArray<PageBridge>()
            if not (isNull model.Edges) then
                for edge in model.Edges do
                    if not (obj.ReferenceEquals(edge, null))
                       && not (String.IsNullOrWhiteSpace edge.SourceId)
                       && not (String.IsNullOrWhiteSpace edge.TargetId) then
                        let connectorId =
                            if String.IsNullOrWhiteSpace edge.Id then
                                sprintf "%s->%s" edge.SourceId edge.TargetId
                            else
                                edge.Id

                        match nodeLookup.TryGetValue edge.SourceId, nodeLookup.TryGetValue edge.TargetId with
                        | (true, srcNode), (true, dstNode) ->
                            let srcModuleId =
                                match nodeToSegment.TryGetValue edge.SourceId with
                                | true, segId -> segId
                                | _ -> resolveModuleId srcNode tiers tiersSet
                            let dstModuleId =
                                match nodeToSegment.TryGetValue edge.TargetId with
                                | true, segId -> segId
                                | _ -> resolveModuleId dstNode tiers tiersSet
                            let resolvePage moduleId fallback =
                                let candidate = if String.IsNullOrWhiteSpace moduleId then String.Empty else moduleId.Trim()
                                let findInPlans candidate =
                                    pagePlans
                                    |> Array.tryPick (fun plan ->
                                        if isNull plan.Modules then None
                                        else
                                            if plan.Modules
                                               |> Array.exists (fun m -> m.Equals(candidate, StringComparison.OrdinalIgnoreCase)) then
                                                Some plan.PageIndex
                                            else
                                                None)

                                match moduleToPage.TryGetValue candidate with
                                | true, page -> page
                                | _ ->
                                    match findInPlans candidate with
                                    | Some page -> page
                                    | None ->
                                        match segmentOrigins.TryGetValue candidate with
                                        | true, (originalId, _, _) when not (String.IsNullOrWhiteSpace originalId) ->
                                            let trimmed = originalId.Trim()
                                            match moduleToPage.TryGetValue trimmed with
                                            | true, page -> page
                                            | _ ->
                                                match findInPlans trimmed with
                                                | Some page -> page
                                                | None -> fallback
                                        | _ -> fallback

                            let srcPage = resolvePage srcModuleId 0
                            let dstPage = resolvePage dstModuleId srcPage
                            if srcPage <> dstPage then
                                let exitAnchor, entryAnchor =
                                    if moduleBounds.ContainsKey srcModuleId && moduleBounds.ContainsKey dstModuleId then
                                        let exitPt = computeAnchor moduleBounds[srcModuleId] moduleBounds[dstModuleId]
                                        let entryPt = computeAnchor moduleBounds[dstModuleId] moduleBounds[srcModuleId]
                                        exitPt, entryPt
                                    else
                                        let srcCenter =
                                            match placementCenters.TryGetValue edge.SourceId with
                                            | true, value -> value
                                            | _ -> point 0.f 0.f
                                        let dstCenter =
                                            match placementCenters.TryGetValue edge.TargetId with
                                            | true, value -> value
                                            | _ -> point 0.f 0.f
                                        srcCenter, dstCenter

                                let metadataCopy =
                                    if isNull edge.Metadata || edge.Metadata.Count = 0 then
                                        Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>
                                    else
                                        Dictionary<string, string>(edge.Metadata, StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>

                                pageBridges.Add(
                                    { BridgeId = sprintf "%s#page" connectorId
                                      SourcePage = srcPage
                                      TargetPage = dstPage
                                      SourceModuleId = srcModuleId
                                      TargetModuleId = dstModuleId
                                      SourceNodeId = edge.SourceId
                                      TargetNodeId = edge.TargetId
                                      ConnectorId = connectorId
                                      Metadata = metadataCopy
                                      EntryAnchor = entryAnchor
                                      ExitAnchor = exitAnchor })
                        | _ -> ()

            let layerAssignment: PagingPlanner.LayerAssignment =
                if moduleStats.Length = 0 then
                    { Plans = Array.empty
                      ModuleLayers = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _> }
                else
                    let layerOptions = buildLayerSplitOptions model
                    PagingPlanner.computeLayers layerOptions dataset

            let moduleLayerIndex = layerAssignment.ModuleLayers
            let layerShapeTotals = Dictionary<int, int>()
            if moduleStats.Length > 0 then
                for stats in moduleStats do
                    match moduleLayerIndex.TryGetValue(stats.ModuleId) with
                    | true, layerIdx ->
                        let share = Math.Max(1, stats.NodeCount + 1)
                        let existing =
                            match layerShapeTotals.TryGetValue layerIdx with
                            | true, value -> value
                            | _ -> 0
                        layerShapeTotals[layerIdx] <- existing + share
                    | _ -> ()

            let layerConnectorTotals = Dictionary<int, int>()
            let layerBridges = ResizeArray<LayerBridge>()

            if not (isNull model.Edges) then
                for edge in model.Edges do
                    if not (obj.ReferenceEquals(edge, null))
                       && not (String.IsNullOrWhiteSpace edge.SourceId)
                       && not (String.IsNullOrWhiteSpace edge.TargetId) then
                        match placementCenters.TryGetValue edge.SourceId, placementCenters.TryGetValue edge.TargetId with
                        | (true, srcCenter), (true, dstCenter) ->
                            let srcNode = nodeLookup[edge.SourceId]
                            let dstNode = nodeLookup[edge.TargetId]
                            let srcModuleId =
                                match nodeToSegment.TryGetValue edge.SourceId with
                                | true, segId -> segId
                                | _ -> resolveModuleId srcNode tiers tiersSet
                            let dstModuleId =
                                match nodeToSegment.TryGetValue edge.TargetId with
                                | true, segId -> segId
                                | _ -> resolveModuleId dstNode tiers tiersSet
                            match moduleLayerIndex.TryGetValue(srcModuleId), moduleLayerIndex.TryGetValue(dstModuleId) with
                            | (true, srcLayer), (true, dstLayer) ->
                                if srcLayer = dstLayer then
                                    let current =
                                        match layerConnectorTotals.TryGetValue srcLayer with
                                        | true, total -> total
                                        | _ -> 0
                                    layerConnectorTotals[srcLayer] <- current + 1
                                else
                                    let hasBounds =
                                        moduleBounds.ContainsKey srcModuleId && moduleBounds.ContainsKey dstModuleId
                                    let exitPt, entryPt =
                                        if hasBounds then
                                            computeAnchor moduleBounds[srcModuleId] moduleBounds[dstModuleId],
                                            computeAnchor moduleBounds[dstModuleId] moduleBounds[srcModuleId]
                                        else
                                            srcCenter, dstCenter
                                    let connectorId =
                                        if String.IsNullOrWhiteSpace edge.Id then
                                            $"{edge.SourceId}->{edge.TargetId}"
                                        else
                                            edge.Id
                                    let currentSrc =
                                        match layerConnectorTotals.TryGetValue srcLayer with
                                        | true, total -> total
                                        | _ -> 0
                                    layerConnectorTotals[srcLayer] <- currentSrc + 1
                                    let currentDst =
                                        match layerConnectorTotals.TryGetValue dstLayer with
                                        | true, total -> total
                                        | _ -> 0
                                    layerConnectorTotals[dstLayer] <- currentDst + 1
                                    let metadataCopy =
                                        if edge.Metadata = null || edge.Metadata.Count = 0 then
                                            Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>
                                        else
                                            Dictionary<string, string>(edge.Metadata, StringComparer.OrdinalIgnoreCase) :> IDictionary<_, _>
                                    layerBridges.Add(
                                        { BridgeId = $"{connectorId}#layer"
                                          SourceLayer = srcLayer
                                          SourceNodeId = edge.SourceId
                                          TargetLayer = dstLayer
                                          TargetNodeId = edge.TargetId
                                          ConnectorId = connectorId
                                          Metadata = metadataCopy
                                          EntryAnchor = entryPt
                                          ExitAnchor = exitPt })
                            | _ -> ()
                        | _ -> ()

            let updatedLayerPlans =
                if layerAssignment.Plans.Length = 0 then
                    Array.empty
                else
                    layerAssignment.Plans
                    |> Array.map (fun plan ->
                        let shapes =
                            match layerShapeTotals.TryGetValue(plan.LayerIndex) with
                            | true, value -> value
                            | _ -> plan.ShapeCount
                        let connectors =
                            match layerConnectorTotals.TryGetValue(plan.LayerIndex) with
                            | true, value -> value
                            | _ -> plan.ConnectorCount
                        { plan with
                            ShapeCount = shapes
                            ConnectorCount = connectors })

            let bridgeArray = layerBridges.ToArray()

            let pageLayouts =
                orderedPagePlans
                |> Array.map (fun plan ->
                    let boundsOpt =
                        match pageExtents.TryGetValue plan.PageIndex with
                        | true, bounds when not (Single.IsPositiveInfinity bounds[0]) && not (Single.IsPositiveInfinity bounds[1]) ->
                            Some bounds
                        | _ -> None
                    match boundsOpt with
                    | Some bounds ->
                        let width = bounds[2] - bounds[0]
                        let height = bounds[3] - bounds[1]
                        { PageIndex = plan.PageIndex
                          Origin = point bounds[0] bounds[1]
                          Width = if width > 0.f then width else 0.f
                          Height = if height > 0.f then height else 0.f
                          BodyHeight = pageBodyHeight }
                    | None ->
                        { PageIndex = plan.PageIndex
                          Origin = point 0.f 0.f
                          Width = 0.f
                          Height = 0.f
                          BodyHeight = pageBodyHeight })

            let stats =
                { NodeCount = nodesArray.Length
                  ConnectorCount = edgesArray.Length
                  ModuleCount = modules.Count
                  ContainerCount = containersArray.Length
                  ModuleIds =
                    modules.Keys
                    |> Seq.filter (fun m -> not (String.IsNullOrWhiteSpace m))
                    |> Seq.map (fun m -> m.Trim())
                    |> Seq.distinct
                    |> Seq.toArray
                  Overflows =
                    truncated
                    |> Seq.map (fun kv -> { ContainerId = kv.Key; HiddenNodeCount = kv.Value })
                    |> Seq.toArray }

            { OutputMode = "view"
              CanvasWidth = canvasWidth
              CanvasHeight = canvasHeight
              PageHeight = pageHeight
              PageMargin = margin
              TitleHeight = titleHeight
              Nodes = nodesArray
              NodeModules = nodeModules
              PageLayouts = pageLayouts
              Containers = containersArray
              Edges = edgesArray
              Pages = pagePlans
              Layers = updatedLayerPlans
              Bridges = bridgeArray
              PageBridges = pageBridges.ToArray()
              Stats = stats }

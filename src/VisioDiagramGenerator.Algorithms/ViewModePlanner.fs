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

        let modules = Dictionary<string, ViewModule>(StringComparer.OrdinalIgnoreCase)
        let moduleEdgeStats = Dictionary<string, ModuleEdgeStats>(StringComparer.OrdinalIgnoreCase)
        for node in model.Nodes do
            if not (isNull node) then
                let moduleId = resolveModuleId node tiers tiersSet
                let viewModule =
                    match modules.TryGetValue moduleId with
                    | true, m -> m
                    | _ ->
                        let tier =
                            match containerTierMap.TryGetValue moduleId with
                            | true, mapped when not (String.IsNullOrWhiteSpace mapped) && tiersSet.Contains(mapped) -> mapped
                            | _ -> resolveTier tiers tiersSet node
                        let created = { Id = moduleId; Tier = tier; Nodes = ResizeArray() }
                        modules[moduleId] <- created
                        created
                viewModule.Nodes.Add node

        if modules.Count = 0 then
            { OutputMode = "view"
              CanvasWidth = 0.f
              CanvasHeight = 0.f
              Nodes = Array.empty
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
            let nodeLayouts = ResizeArray<NodeLayout>()
            let placementCenters = Dictionary<string, PointF>(StringComparer.OrdinalIgnoreCase)
            let containerLayouts = ResizeArray<ContainerLayout>()
            let truncated = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)

            let tierBuckets = Dictionary<string, ResizeArray<ViewModule>>(StringComparer.OrdinalIgnoreCase)
            for data in modules.Values do
                let bucket =
                    match tierBuckets.TryGetValue data.Tier with
                    | true, existing -> existing
                    | _ ->
                        let created = ResizeArray<ViewModule>()
                        tierBuckets[data.Tier] <- created
                        created
                bucket.Add data

            let slotsPerTier = 6
            let slotsPerRow = 3
            let tierSpacing = 1.4f
            let rowSpacing = 0.85f
            let cardSpacingX = 0.75f
            let cardSpacingY = 0.8f
            let cardPadding = 0.3f
            let nodeWidth = 1.35f
            let nodeHeight = 0.34f
            let columnGap = 0.18f
            let rowGap = 0.08f
            let headerHeight = 0.55f
            let maxRowsPerColumn = 6
            let maxColumns = 2
            let maxCardHeight = 4.0f

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

            for tier in tiers do
                let modulesInTier =
                    match tierBuckets.TryGetValue tier with
                    | true, bucket when bucket.Count > 0 ->
                        bucket
                        |> Seq.sortWith (fun a b -> StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id))
                        |> Seq.toArray
                    | _ -> Array.empty

                if modulesInTier.Length = 0 then
                    cursorY <- cursorY - tierSpacing
                else
                    let mutable slotIndex = 0
                    for viewModule in modulesInTier do
                        let orderedNodes: Node array =
                            viewModule.Nodes
                            |> Seq.filter (fun n -> not (isNull n))
                            |> Seq.sortWith (fun a b ->
                                let primary = StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label)
                                if primary <> 0 then primary
                                else StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id))
                            |> Seq.toArray

                        if orderedNodes.Length = 0 then
                            slotIndex <- slotIndex + 1
                        else
                            let totalNodes = orderedNodes.Length
                            let maxVisible = maxRowsPerColumn * maxColumns
                            let mutable visibleCount = Math.Min(totalNodes, maxVisible)
                            let requiredColumns = int (Math.Ceiling(float visibleCount / float maxRowsPerColumn))
                            let columns = Math.Min(maxColumns, Math.Max(1, requiredColumns))
                            let mutable rows = int (Math.Ceiling(float visibleCount / float columns))
                            rows <- Math.Max(1, Math.Min(maxRowsPerColumn, rows))

                            let horizontalGap = if columns > 1 then float32 (columns - 1) * columnGap else 0.f
                            let cardWidth = cardPadding * 2.f + (float32 columns * nodeWidth) + horizontalGap

                            let verticalGap = if rows > 1 then float32 (rows - 1) * rowGap else 0.f
                            let listHeight = float32 rows * nodeHeight + verticalGap

                            let mutable cardHeight = cardPadding + headerHeight + rowGap + listHeight + cardPadding
                            let mutable overflow = totalNodes - visibleCount

                            if cardHeight > maxCardHeight then
                                let usableList = maxCardHeight - (cardPadding + headerHeight + rowGap + cardPadding)
                                let rowsThatFit =
                                    Math.Max(
                                        1,
                                        int (
                                            Math.Floor(
                                                (float usableList + float rowGap)
                                                / (float nodeHeight + float rowGap))))
                                visibleCount <- Math.Min(totalNodes, rowsThatFit * columns)
                                overflow <- totalNodes - visibleCount
                                cardHeight <- maxCardHeight

                            let slotRow = slotIndex / slotsPerRow
                            let slotCol = slotIndex % slotsPerRow
                            let left = float32 slotCol * (cardWidth + cardSpacingX)
                            let bottom = cursorY - float32 slotRow * (cardHeight + cardSpacingY)
                            let bodyTop = bottom + cardHeight - headerHeight - rowGap

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

                            if overflow > 0 then
                                truncated[viewModule.Id] <- overflow
                                let badgeText = sprintf "+%d." overflow
                                let badgeWidth = fmax 0.9f (float32 badgeText.Length * 0.1f)
                                let badgeLeft = left + cardWidth - cardPadding - badgeWidth
                                let badgeBottom = bottom + cardPadding * 0.5f
                                let badgeLayout =
                                    { Id = $"{viewModule.Id}#overflow"
                                      Position = point badgeLeft badgeBottom
                                      Size = Nullable<Size>(Size(badgeWidth, 0.3f)) }
                                nodeLayouts.Add badgeLayout

                            let label =
                                if overflow > 0 then
                                    sprintf "%s (+%d)" viewModule.Id overflow
                                else
                                    viewModule.Id

                            containerLayouts.Add(
                                { Id = viewModule.Id
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

                    if slotIndex > 0 then
                        let usedRows = int (Math.Ceiling(float slotIndex / float slotsPerRow))
                        cursorY <- cursorY - (float32 usedRows * (headerHeight + cardPadding + rowSpacing))

                    cursorY <- cursorY - tierSpacing

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
                        let srcModuleId = resolveModuleId srcNode tiers tiersSet
                        let dstModuleId = resolveModuleId dstNode tiers tiersSet
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
                    let pageOptions = buildPageSplitOptions model usableHeight
                    PagingPlanner.computePages pageOptions dataset

            let moduleToPage = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            for plan in pagePlans do
                if not (isNull (box plan)) && not (isNull plan.Modules) then
                    for moduleId in plan.Modules do
                        if not (String.IsNullOrWhiteSpace moduleId) then
                            let trimmed = moduleId.Trim()
                            if not (moduleToPage.ContainsKey trimmed) then
                                moduleToPage[trimmed] <- plan.PageIndex

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
                            let srcModuleId = resolveModuleId srcNode tiers tiersSet
                            let dstModuleId = resolveModuleId dstNode tiers tiersSet
                            let srcPage =
                                match moduleToPage.TryGetValue srcModuleId with
                                | true, page -> page
                                | _ -> 0
                            let dstPage =
                                match moduleToPage.TryGetValue dstModuleId with
                                | true, page -> page
                                | _ -> srcPage
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
                            let srcModuleId = resolveModuleId srcNode tiers tiersSet
                            let dstModuleId = resolveModuleId dstNode tiers tiersSet
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
              Nodes = nodesArray
              Containers = containersArray
              Edges = edgesArray
              Pages = pagePlans
              Layers = updatedLayerPlans
              Bridges = bridgeArray
              PageBridges = pageBridges.ToArray()
              Stats = stats }

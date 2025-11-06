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

    type private ChannelLabelBucket =
        { PageIndex: int
          Channel: EdgeChannel
          mutable StubStart: PointF
          mutable StubEnd: PointF
          mutable LabelCenter: PointF
          Labels: ResizeArray<string>
          LabelSet: HashSet<string>
          mutable OverflowCount: int
          mutable SampleCount: int }

    type private SegmentMetrics =
        { Columns: int
          CardWidth: float32
          CardHeight: float32
          VisibleCount: int
          Overflow: int }

    type private LaneCapacityOptions =
        { MaxModulesPerLane: int
          MaxNodesPerLane: int
          MaxConnectorsPerLane: int
          MaxLaneHeightIn: float
          RowSpacingIn: float32 }

    type private LaneState =
        { mutable ModuleCount: int
          mutable NodeCount: int
          mutable ConnectorCount: int
          mutable Height: float }

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

    let private getMetadataSingle (model: DiagramModel) (key: string) =
        getMetadataDouble model key |> Option.map float32

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
              RowLayouts = Array.empty
              Edges = Array.empty
              Pages = Array.empty
              Layers = Array.empty
              ChannelLabels = Array.empty
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
            let slotsPerRow =
                getMetadataInt model "layout.view.slotsPerRow"
                |> Option.filter (fun v -> v >= 1)
                |> Option.defaultValue 2
            let baseSlotsPerTier =
                getMetadataInt model "layout.view.slotsPerTier"
                |> Option.filter (fun v -> v >= slotsPerRow)
                |> Option.defaultValue (Math.Max(slotsPerRow * 3, 6))
            let explicitMaxModulesPerLane =
                getMetadataInt model "layout.view.maxModulesPerLane"
                |> Option.filter (fun v -> v >= 1)
            let mutable maxModulesPerLane =
                explicitMaxModulesPerLane |> Option.defaultValue (Math.Max(slotsPerRow * 3, 12))
            let mutable slotsPerTier =
                Math.Max(slotsPerRow, Math.Min(baseSlotsPerTier, maxModulesPerLane))
            let mutable tierSpacing =
                getMetadataSingle model "layout.view.tierSpacingIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue 4.5f
            let mutable rowSpacing =
                getMetadataSingle model "layout.view.rowSpacingIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue 3.0f
            let mutable cardSpacingX =
                getMetadataSingle model "layout.view.cardSpacingXIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue 6.0f
            let mutable cardSpacingY =
                getMetadataSingle model "layout.view.cardSpacingYIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue 4.0f
            let cardPadding =
                getMetadataSingle model "layout.view.cardPaddingIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue 0.5f
            let baseNodeWidth =
                getMetadataSingle model "layout.view.baseNodeWidthIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue 1.55f
            let minNodeChars = 16
            let maxNodeChars = 64
            let charWidth = 0.075f
            let maxNodeWidth = 4.5f
            let nodeHeight =
                getMetadataSingle model "layout.view.nodeHeightIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue 0.42f
            let columnGap =
                getMetadataSingle model "layout.view.columnGapIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue 0.6f
            let rowGap =
                getMetadataSingle model "layout.view.rowGapIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue 0.28f
            let headerHeight = 0.55f
            let maxRowsPerColumn =
                getMetadataInt model "layout.view.maxRowsPerColumn"
                |> Option.filter (fun v -> v >= 1)
                |> Option.defaultValue 5
            let maxColumns =
                getMetadataInt model "layout.view.maxColumns"
                |> Option.filter (fun v -> v >= 1)
                |> Option.defaultValue 3
            let maxCardHeight =
                getMetadataSingle model "layout.view.maxCardHeightIn"
                |> Option.filter (fun v -> v >= nodeHeight + rowGap + headerHeight + (cardPadding * 2.f))
                |> Option.defaultValue 5.5f

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

            let limitByPage =
                getMetadataBool model "layout.view.limitByPage"
                |> Option.defaultValue false

            let preLayoutUsableHeight =
                if limitByPage then computeUsableHeight model 0.f
                else None
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

            if explicitMaxModulesPerLane.IsNone then
                let preferred = Math.Max(maxModulesPerLane, slotsPerRow * maxRowsByHeight)
                if preferred <> maxModulesPerLane then
                    maxModulesPerLane <- preferred
                    slotsPerTier <- Math.Max(slotsPerRow, Math.Min(baseSlotsPerTier, maxModulesPerLane))

            let segmentCapacity = Math.Max(1, maxRowsByHeight * Math.Max(1, maxColumns))

            let laneNodeCapDefault =
                let baseline = slotsPerRow * maxRowsByHeight
                Math.Max(baseline, 12)

            let explicitLaneNodeCap =
                getMetadataInt model "layout.view.maxNodesPerLane"
                |> Option.filter (fun v -> v > 0)
            let laneNodeCap =
                explicitLaneNodeCap |> Option.defaultValue laneNodeCapDefault

            let explicitLaneConnectorCap =
                getMetadataInt model "layout.view.maxConnectorsPerLane"
                |> Option.filter (fun v -> v > 0)
            let laneConnectorCap =
                explicitLaneConnectorCap |> Option.defaultValue 400

            let laneHeightLimit =
                getMetadataDouble model "layout.view.maxLaneHeightIn"
                |> Option.filter (fun v -> v > 0.0)
                |> Option.defaultValue Double.PositiveInfinity

            let laneCapacity =
                { MaxModulesPerLane = maxModulesPerLane
                  MaxNodesPerLane = laneNodeCap
                  MaxConnectorsPerLane = laneConnectorCap
                  MaxLaneHeightIn = laneHeightLimit
                  RowSpacingIn = rowSpacing }

            let rowModuleLimit =
                let laneLimit =
                    if laneCapacity.MaxModulesPerLane > 0 then laneCapacity.MaxModulesPerLane
                    else Int32.MaxValue
                let limit = Math.Min(slotsPerRow, laneLimit)
                if limit <= 0 then 1 else limit
            let rowNodeLimit =
                if laneCapacity.MaxNodesPerLane > 0 then laneCapacity.MaxNodesPerLane
                else Int32.MaxValue
            let rowConnectorLimit =
                if laneCapacity.MaxConnectorsPerLane > 0 then laneCapacity.MaxConnectorsPerLane
                else Int32.MaxValue

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
            let moduleTierIndex = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)

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

                let rec adjustSegmentSize requested =
                    let candidate = Math.Max(1, requested)
                    let metrics = computeSegmentMetrics candidate
                    if metrics.Overflow <= 0 then
                        candidate, metrics
                    else
                        let nextVisible = metrics.VisibleCount
                        let proposed =
                            if nextVisible > 0 && nextVisible < candidate then nextVisible
                            else candidate - 1
                        let nextCandidate = Math.Max(1, proposed)
                        if nextCandidate >= candidate then
                            if candidate <= 1 then
                                1, computeSegmentMetrics 1
                            else
                                adjustSegmentSize (candidate - 1)
                        else
                            adjustSegmentSize nextCandidate

                let segmentsForModule =
                    if orderedNodes.Length = 0 then
                        [| (Array.empty<Node>, computeSegmentMetrics 0) |]
                    else
                        let segments = ResizeArray<Node[] * SegmentMetrics>()
                        let mutable startIdx = 0
                        while startIdx < orderedNodes.Length do
                            let remaining = orderedNodes.Length - startIdx
                            let requested = Math.Min(segmentCapacity, remaining)
                            let requested = if requested <= 0 then 1 else requested
                            let takeCount, metrics = adjustSegmentSize requested
                            let takeCount = Math.Min(remaining, Math.Max(1, takeCount))
                            let slice = orderedNodes.[startIdx .. (startIdx + takeCount - 1)]
                            segments.Add(slice, metrics)
                            startIdx <- startIdx + takeCount
                        segments.ToArray()

                let segmentCount = segmentsForModule.Length
                for idxSeg = 0 to segmentCount - 1 do
                    let (chunk, metrics) = segmentsForModule[idxSeg]
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

            let channelLabelBuckets = Dictionary<(int * string * int * string), ChannelLabelBucket>(HashIdentity.Structural)
            let maxChannelLabelLines = 12
            let ensureChannelLabelBucket pageIndex (channel: EdgeChannel) (callout: EdgeCallout) =
                let key = (pageIndex, channel.Key, channel.BundleIndex, channel.Orientation)
                match channelLabelBuckets.TryGetValue key with
                | true, bucket -> bucket
                | _ ->
                    let bucket =
                        { PageIndex = pageIndex
                          Channel = channel
                          StubStart = callout.StubStart
                          StubEnd = callout.StubEnd
                          LabelCenter = callout.LabelCenter
                          Labels = ResizeArray<string>()
                          LabelSet = HashSet<string>(StringComparer.Ordinal)
                          OverflowCount = 0
                          SampleCount = 0 }
                    channelLabelBuckets[key] <- bucket
                    bucket
                |> fun bucket ->
                    let countPrev = float32 bucket.SampleCount
                    let countNew = countPrev + 1.0f
                    if bucket.SampleCount > 0 then
                        bucket.StubStart <- point ((bucket.StubStart.X * countPrev + callout.StubStart.X) / countNew)
                                                  ((bucket.StubStart.Y * countPrev + callout.StubStart.Y) / countNew)
                        bucket.StubEnd <- point ((bucket.StubEnd.X * countPrev + callout.StubEnd.X) / countNew)
                                                ((bucket.StubEnd.Y * countPrev + callout.StubEnd.Y) / countNew)
                        bucket.LabelCenter <- point ((bucket.LabelCenter.X * countPrev + callout.LabelCenter.X) / countNew)
                                                    ((bucket.LabelCenter.Y * countPrev + callout.LabelCenter.Y) / countNew)
                    bucket.SampleCount <- bucket.SampleCount + 1
                    bucket

            let enforceLaneCapacity (plans: PagePlan array) =
                if isNull (box plans) || plans.Length = 0 then
                    plans
                else
                    let sortedPlans =
                        plans
                        |> Array.filter (fun p -> not (isNull (box p)))
                        |> Array.sortBy (fun p -> p.PageIndex)
                    let result = ResizeArray<PagePlan>()
                    let laneStates = Dictionary<string, LaneState>(StringComparer.OrdinalIgnoreCase)
                    let mutable currentModules = ResizeArray<string>()
                    let mutable currentConnectors = 0
                    let mutable currentNodes = 0
                    let mutable currentMaxOccupancy = 0.0
                    let mutable nextPageIndex = 0
                    let rowSpacingDelta = float laneCapacity.RowSpacingIn

                    let resetPage () =
                        laneStates.Clear()
                        currentModules <- ResizeArray<string>()
                        currentConnectors <- 0
                        currentNodes <- 0
                        currentMaxOccupancy <- 0.0

                    let flushPage () =
                        if currentModules.Count > 0 then
                            result.Add(
                                { PageIndex = nextPageIndex
                                  Modules = currentModules.ToArray()
                                  Connectors = currentConnectors
                                  Nodes = currentNodes
                                  Occupancy = currentMaxOccupancy })
                            nextPageIndex <- nextPageIndex + 1
                            resetPage()

                    let baseModulesLimit =
                        if laneCapacity.MaxModulesPerLane <= 0 then Int32.MaxValue
                        else laneCapacity.MaxModulesPerLane
                    let baseNodesLimit =
                        if laneCapacity.MaxNodesPerLane <= 0 then Int32.MaxValue
                        else laneCapacity.MaxNodesPerLane
                    let baseConnectorsLimit =
                        if laneCapacity.MaxConnectorsPerLane <= 0 then Int32.MaxValue
                        else laneCapacity.MaxConnectorsPerLane
                    let maxModulesLimit =
                        if explicitMaxModulesPerLane.IsSome && slotsPerRow > baseModulesLimit then Int32.MaxValue
                        else baseModulesLimit
                    let maxNodesLimit =
                        if explicitLaneNodeCap.IsSome then Int32.MaxValue
                        else baseNodesLimit
                    let maxConnectorsLimit =
                        if explicitLaneConnectorCap.IsSome then Int32.MaxValue
                        else baseConnectorsLimit
                    let maxHeightLimit =
                        if laneCapacity.MaxLaneHeightIn <= 0.0 then Double.PositiveInfinity
                        else laneCapacity.MaxLaneHeightIn

                    let rec addModule (moduleId: string) =
                        if String.IsNullOrWhiteSpace moduleId then
                            ()
                        else
                            let trimmedId = moduleId.Trim()
                            let viewModuleOpt =
                                match modules.TryGetValue trimmedId with
                                | true, value -> Some value
                                | _ -> None
                            let tier =
                                match viewModuleOpt with
                                | Some vm when not (String.IsNullOrWhiteSpace vm.Tier) -> vm.Tier
                                | _ -> String.Empty
                            let nodeCount =
                                match viewModuleOpt with
                                | Some vm -> vm.Nodes.Count
                                | _ -> 0
                            let connectorCount =
                                match segmentEdgeStats.TryGetValue trimmedId with
                                | true, stats -> stats.ConnectorCount
                                | _ -> 0
                            let metrics =
                                match segmentMetrics.TryGetValue trimmedId with
                                | true, value -> value
                                | _ ->
                                    match viewModuleOpt with
                                    | Some vm ->
                                        let computed = computeSegmentMetrics vm.Nodes.Count
                                        segmentMetrics[trimmedId] <- computed
                                        computed
                                    | None ->
                                        let computed = computeSegmentMetrics nodeCount
                                        segmentMetrics[trimmedId] <- computed
                                        computed
                            let cardHeight = float metrics.CardHeight
                            let laneState =
                                match laneStates.TryGetValue tier with
                                | true, state -> state
                                | _ ->
                                    let state = { ModuleCount = 0; NodeCount = 0; ConnectorCount = 0; Height = 0.0 }
                                    laneStates[tier] <- state
                                    state

                            let pendingModules = laneState.ModuleCount + 1
                            let pendingNodes = laneState.NodeCount + nodeCount
                            let pendingConnectors = laneState.ConnectorCount + connectorCount
                            let additionalHeight =
                                if laneState.ModuleCount > 0 then cardHeight + rowSpacingDelta else cardHeight
                            let pendingHeight = laneState.Height + additionalHeight
                            let laneWouldOverflow =
                                (pendingModules > maxModulesLimit)
                                || (pendingNodes > maxNodesLimit)
                                || (pendingConnectors > maxConnectorsLimit)
                                || (pendingHeight > maxHeightLimit)

                            if currentModules.Count > 0 && laneWouldOverflow then
                                flushPage()
                                addModule trimmedId
                            else
                                laneState.ModuleCount <- pendingModules
                                laneState.NodeCount <- pendingNodes
                                laneState.ConnectorCount <- pendingConnectors
                                laneState.Height <- pendingHeight
                                currentModules.Add trimmedId
                                currentConnectors <- currentConnectors + connectorCount
                                currentNodes <- currentNodes + nodeCount

                                if not (Double.IsInfinity maxHeightLimit) && pendingHeight > 0.0 then
                                    let occupancy = (pendingHeight / maxHeightLimit) * 100.0
                                    if occupancy > currentMaxOccupancy then
                                        currentMaxOccupancy <- occupancy
                                elif maxNodesLimit <> Int32.MaxValue && pendingNodes > 0 then
                                    let occupancy = (float pendingNodes / float maxNodesLimit) * 100.0
                                    if occupancy > currentMaxOccupancy then
                                        currentMaxOccupancy <- occupancy

                    for plan in sortedPlans do
                        if not (isNull plan.Modules) then
                            for moduleId in plan.Modules do
                                addModule moduleId
                        flushPage()

                    if result.Count = 0 then plans else result.ToArray()

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

            let basePagePlans =
                if initialPagePlans.Length = 0 && orderedSegments.Count > 0 then
                    [| { PageIndex = 0
                         Modules = orderedSegments.ToArray()
                         Connectors = 0
                         Nodes = 0
                         Occupancy = 0.0 } |]
                else
                    initialPagePlans

            let enforcedPagePlans = enforceLaneCapacity basePagePlans

            let plannedPagePlans =
                if enforcedPagePlans.Length = 0 then basePagePlans else enforcedPagePlans

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
            let rowLayouts = ResizeArray<RowLayout>()

            let partitionTierRows (tierModules: string array) =
                if isNull (box tierModules) || tierModules.Length = 0 then
                    Array.empty
                else
                    let rows = ResizeArray<string[]>()
                    let mutable currentRow = ResizeArray<string>()
                    let mutable rowModules = 0
                    let mutable rowNodes = 0
                    let mutable rowConnectors = 0

                    let flushRow () =
                        if currentRow.Count > 0 then
                            rows.Add(currentRow.ToArray())
                            currentRow <- ResizeArray<string>()
                            rowModules <- 0
                            rowNodes <- 0
                            rowConnectors <- 0

                    for segmentId in tierModules do
                        if not (String.IsNullOrWhiteSpace segmentId) then
                            let moduleNodes =
                                match modules.TryGetValue segmentId with
                                | true, vm -> vm.Nodes.Count
                                | _ -> 0
                            let moduleConnectors =
                                match segmentEdgeStats.TryGetValue segmentId with
                                | true, stats -> stats.ConnectorCount
                                | _ -> 0

                            let overflowBeforeAdd =
                                (rowModules > 0 && rowModules + 1 > rowModuleLimit)
                                || (rowModules > 0 && rowNodeLimit <> Int32.MaxValue && rowNodes + moduleNodes > rowNodeLimit)
                                || (rowModules > 0 && rowConnectorLimit <> Int32.MaxValue && rowConnectors + moduleConnectors > rowConnectorLimit)

                            if overflowBeforeAdd then
                                flushRow()

                            currentRow.Add segmentId
                            rowModules <- rowModules + 1
                            rowNodes <- rowNodes + moduleNodes
                            rowConnectors <- rowConnectors + moduleConnectors

                            let rowReachedCapacity =
                                (rowModules >= rowModuleLimit)
                                || (rowNodeLimit <> Int32.MaxValue && rowNodes >= rowNodeLimit)
                                || (rowConnectorLimit <> Int32.MaxValue && rowConnectors >= rowConnectorLimit)

                            if rowReachedCapacity then
                                flushRow()

                    flushRow()
                    let result = rows.ToArray()
                    result

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

            let mutable pageSpacing =
                getMetadataSingle model "layout.view.pageSpacingIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue (tierSpacing * 2.f)

            let distinctPageCount =
                if plannedPagePlans.Length = 0 then 1
                else
                    plannedPagePlans
                    |> Array.map (fun p -> p.PageIndex)
                    |> Array.distinct
                    |> Array.length
            let avgModulesPerPage =
                if distinctPageCount <= 0 then float32 modules.Count
                else float32 modules.Count / float32 distinctPageCount
            let densityScale =
                if Single.IsNaN avgModulesPerPage || avgModulesPerPage <= 6.f then 1.f
                else
                    let bump = (avgModulesPerPage - 6.f) / 4.f
                    let limited = if bump > 1.6f then 1.6f else bump
                    1.f + limited
            if densityScale > 1.f then
                tierSpacing <- tierSpacing * densityScale
                let rowFactor = if densityScale < 1.25f then densityScale else 1.25f
                rowSpacing <- rowSpacing * rowFactor
                cardSpacingX <- cardSpacingX * densityScale
                let yFactor = if densityScale < 1.35f then densityScale else 1.35f
                cardSpacingY <- cardSpacingY * yFactor
                let pageFactor = if densityScale < 1.4f then densityScale else 1.4f
                pageSpacing <- pageSpacing * pageFactor
                let tightened =
                    Math.Ceiling(float slotsPerTier / float densityScale)
                    |> int
                slotsPerTier <- Math.Max(slotsPerRow, Math.Max(1, tightened))

            let defaultPageMargin = 1.0
            let margin =
                if limitByPage then
                    float32 (getMetadataDouble model "layout.page.marginIn" |> Option.defaultValue defaultPageMargin)
                else
                    0.f
            let titleHeight =
                if limitByPage && hasTitle model then 0.6f else 0.f
            let pageHeight =
                if limitByPage then
                    match getMetadataDouble model "layout.page.heightIn" with
                    | Some value when value > 0.0 -> float32 value
                    | _ -> 0.f
                else
                    0.f
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
                let mutable pageRowIndex = 0
                let rowIndexForTier = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                let tierMap =
                    match segmentsByPageTier.TryGetValue pageIndex with
                    | true, value -> value
                    | _ -> Dictionary<string, ResizeArray<string>>(StringComparer.OrdinalIgnoreCase)

                for tier in tiers do
                    let mutable tierRowIndex =
                        match rowIndexForTier.TryGetValue tier with
                        | true, value -> value
                        | _ -> 0
                    let modulesInTier =
                        match tierMap.TryGetValue tier with
                        | true, bucket when bucket.Count > 0 -> bucket |> Seq.toArray
                        | _ -> Array.empty

                    if modulesInTier.Length = 0 then
                        cursorY <- cursorY - tierSpacing
                    else
                        let rows = partitionTierRows modulesInTier
                        let mutable accumulatedHeight = 0.f

                        for rowIdx = 0 to rows.Length - 1 do
                            let rowSegments = rows[rowIdx]
                            if rowSegments.Length > 0 then
                                let rowTop = cursorY - accumulatedHeight
                                let rowHeight =
                                    rowSegments
                                    |> Array.choose (fun segId ->
                                        match segmentMetrics.TryGetValue segId with
                                        | true, metrics -> Some metrics.CardHeight
                                        | _ ->
                                            match modules.TryGetValue segId with
                                            | true, moduleInfo ->
                                                let computed = computeSegmentMetrics moduleInfo.Nodes.Count
                                                segmentMetrics[segId] <- computed
                                                Some computed.CardHeight
                                            | _ -> None)
                                    |> function
                                        | [||] -> 0.f
                                        | arr -> arr |> Array.max

                                let mutable currentLeft = 0.f

                                for segmentId in rowSegments do
                                    match modules.TryGetValue segmentId with
                                    | true, viewModule ->
                                        let orderedNodes =
                                            viewModule.Nodes
                                            |> Seq.filter (fun n -> not (isNull n))
                                            |> Seq.toArray

                                        if orderedNodes.Length = 0 then
                                            currentLeft <- currentLeft + cardSpacingX
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

                                            let left = currentLeft
                                            let bottom = cursorY - accumulatedHeight - cardHeight
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
                                            let tierIndex =
                                                tiers
                                                |> Array.tryFindIndex (fun t -> t.Equals(viewModule.Tier, StringComparison.OrdinalIgnoreCase))
                                                |> Option.defaultValue 0
                                            moduleTierIndex[segmentId] <- tierIndex

                                            updateExtents left bottom cardWidth cardHeight

                                            let spacingMultiplier =
                                                match segmentEdgeStats.TryGetValue segmentId with
                                                | true, stats ->
                                                    let baseVisible = Math.Max(1, metrics.VisibleCount)
                                                    let density = float stats.ConnectorCount / float baseVisible
                                                    let scaled = 1.0 + Math.Min(density / 8.0, 3.0)
                                                    float32 scaled
                                                | _ -> 1.f

                                            currentLeft <- currentLeft + cardWidth + (cardSpacingX * spacingMultiplier)
                                    | _ -> ()

                                if rowHeight > 0.f then
                                    let rowBottom = rowTop - rowHeight
                                    rowLayouts.Add(
                                        { PageIndex = pageIndex
                                          Tier = tier
                                          RowIndex = pageRowIndex
                                          TierRowIndex = tierRowIndex
                                          Top = rowTop
                                          Bottom = rowBottom
                                          Height = rowHeight })
                                    tierRowIndex <- tierRowIndex + 1
                                    pageRowIndex <- pageRowIndex + 1
                                    accumulatedHeight <- accumulatedHeight + rowHeight + cardSpacingY

                        if accumulatedHeight > 0.f then
                            accumulatedHeight <- accumulatedHeight - cardSpacingY

                        cursorY <- cursorY - accumulatedHeight - tierSpacing

                    rowIndexForTier[tier] <- tierRowIndex

                if pageBodyHeight > 0.f then
                    let excess = cursorY - (-pageBodyHeight)
                    if excess < 0.f then
                        cursorY <- cursorY - pageSpacing
                    else
                        cursorY <- cursorY - pageSpacing - excess
                else
                    cursorY <- cursorY - pageSpacing

            let offsetX =
                if Single.IsPositiveInfinity minLeft then 0.f else minLeft
            let offsetY =
                if Single.IsPositiveInfinity minBottom then 0.f else minBottom

            if offsetX <> 0.f || offsetY <> 0.f then
                for idx = 0 to nodeLayouts.Count - 1 do
                    let layout = nodeLayouts[idx]
                    nodeLayouts[idx] <-
                        { layout with
                            Position = point (layout.Position.X - offsetX) (layout.Position.Y - offsetY) }
                for entry in Seq.toArray placementCenters do
                    placementCenters[entry.Key] <- point (entry.Value.X - offsetX) (entry.Value.Y - offsetY)
                for idx = 0 to containerLayouts.Count - 1 do
                    let layout = containerLayouts[idx]
                    let bounds = layout.Bounds
                    containerLayouts[idx] <-
                        { layout with
                            Bounds =
                                { bounds with
                                    Left = bounds.Left - offsetX
                                    Bottom = bounds.Bottom - offsetY } }
                for entry in pageExtents do
                    let bounds = entry.Value
                    bounds[0] <- bounds[0] - offsetX
                    bounds[1] <- bounds[1] - offsetY
                    bounds[2] <- bounds[2] - offsetX
                    bounds[3] <- bounds[3] - offsetY
                minLeft <- minLeft - offsetX
                maxRight <- maxRight - offsetX
                minBottom <- minBottom - offsetY
                maxTop <- maxTop - offsetY
                for idx = 0 to rowLayouts.Count - 1 do
                    let row = rowLayouts[idx]
                    rowLayouts[idx] <-
                        { row with
                            Top = row.Top - offsetY
                            Bottom = row.Bottom - offsetY }

            let moduleBounds = Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase)
            for container in containerLayouts do
                if not (String.IsNullOrWhiteSpace container.Id) then
                    moduleBounds[container.Id] <- container.Bounds

            let nodeLookup = Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase)
            for node in model.Nodes do
                if not (isNull node) && not (String.IsNullOrWhiteSpace node.Id) then
                    nodeLookup[node.Id] <- node

            let edgeRoutes = ResizeArray<EdgeRoute>()
            let corridorSequence = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            let moduleSideChannelBuckets =
                Dictionary<string, Dictionary<string, ResizeArray<string * float32>>>(StringComparer.OrdinalIgnoreCase)

            let registerChannelBucket (moduleId: string) (side: string) (channelKey: string) (sortValue: float32) =
                if String.IsNullOrWhiteSpace moduleId || String.IsNullOrWhiteSpace side || String.IsNullOrWhiteSpace channelKey then
                    ()
                else
                    let sideMap =
                        match moduleSideChannelBuckets.TryGetValue moduleId with
                        | true, existing -> existing
                        | _ ->
                            let created = Dictionary<string, ResizeArray<string * float32>>(StringComparer.OrdinalIgnoreCase)
                            moduleSideChannelBuckets[moduleId] <- created
                            created
                    let entries =
                        match sideMap.TryGetValue side with
                        | true, existing -> existing
                        | _ ->
                            let created = ResizeArray<string * float32>()
                            sideMap[side] <- created
                            created
                    if not (entries |> Seq.exists (fun (key, _) -> key.Equals(channelKey, StringComparison.OrdinalIgnoreCase))) then
                        entries.Add(channelKey, sortValue)

            let getTierIndex moduleId =
                match moduleTierIndex.TryGetValue moduleId with
                | true, idx -> idx
                | _ -> 0

            let computeChannelDescriptor (srcModuleId: string) (dstModuleId: string) (srcCenter: PointF) (dstCenter: PointF) =
                if String.IsNullOrWhiteSpace srcModuleId || String.IsNullOrWhiteSpace dstModuleId then
                    None
                elif srcModuleId.Equals(dstModuleId, StringComparison.OrdinalIgnoreCase) then
                    match moduleBounds.TryGetValue srcModuleId with
                    | true, bounds ->
                        let dx = Math.Abs(float (dstCenter.X - srcCenter.X))
                        let dy = Math.Abs(float (dstCenter.Y - srcCenter.Y))
                        let horizontal = dx >= dy
                        if horizontal then
                            let corridorKey = $"self:{srcModuleId}"
                            let midX = (srcCenter.X + dstCenter.X) / 2.f
                            Some (corridorKey, "loop-horizontal", (if dstCenter.Y >= srcCenter.Y then "top" else "bottom"), (if dstCenter.Y >= srcCenter.Y then "top" else "bottom"), midX, midX)
                        else
                            let corridorKey = $"self:{srcModuleId}"
                            let midY = (srcCenter.Y + dstCenter.Y) / 2.f
                            Some (corridorKey, "loop-vertical", (if dstCenter.X >= srcCenter.X then "right" else "left"), (if dstCenter.X >= srcCenter.X then "right" else "left"), midY, midY)
                    | _ -> None
                else
                    if not (moduleBounds.ContainsKey srcModuleId) || not (moduleBounds.ContainsKey dstModuleId) then
                        None
                    else
                        let srcTier = getTierIndex srcModuleId
                        let dstTier = getTierIndex dstModuleId
                        let srcSide, dstSide =
                            if srcTier = dstTier then
                                if srcCenter.X <= dstCenter.X then
                                    ("right", "left")
                                else
                                    ("left", "right")
                            elif srcTier < dstTier then
                                ("right", "left")
                            else
                                ("left", "right")

                        let corridorKey =
                            if srcTier = dstTier then
                                let leftModule, rightModule =
                                    if srcCenter.X <= dstCenter.X then srcModuleId, dstModuleId
                                    else dstModuleId, srcModuleId
                                sprintf "tier:%d:%s->%s" srcTier leftModule rightModule
                            elif srcTier < dstTier then
                                sprintf "down:%d:%d:%s->%s" srcTier dstTier srcModuleId dstModuleId
                            else
                                sprintf "up:%d:%d:%s->%s" dstTier srcTier dstModuleId srcModuleId

                        let orientation =
                            if srcTier = dstTier then "horizontal" else "vertical"

                        let sortSrc =
                            if srcTier = dstTier then srcCenter.Y else srcCenter.X
                        let sortDst =
                            if srcTier = dstTier then dstCenter.Y else dstCenter.X

                        Some (corridorKey, orientation, srcSide, dstSide, sortSrc, sortDst)

            if not (isNull model.Edges) then
                for edge in model.Edges do
                    if not (isNull edge)
                       && not (String.IsNullOrWhiteSpace edge.SourceId)
                       && not (String.IsNullOrWhiteSpace edge.TargetId) then
                        match placementCenters.TryGetValue edge.SourceId, placementCenters.TryGetValue edge.TargetId with
                        | (true, srcCenter), (true, dstCenter) ->
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
                                match computeChannelDescriptor srcModuleId dstModuleId srcCenter dstCenter with
                                | Some (corridorKey, orientation, srcSide, dstSide, sortSrc, sortDst) ->
                                    registerChannelBucket srcModuleId srcSide corridorKey sortSrc
                                    registerChannelBucket dstModuleId dstSide corridorKey sortDst
                                | None -> ()
                            | _ -> ()
                        | _ -> ()

            let moduleSideSlotMap =
                let map = Dictionary<struct (string * string * string), float32>(HashIdentity.Structural)
                for KeyValue(moduleId, sideMap) in moduleSideChannelBuckets do
                    for KeyValue(side, entries) in sideMap do
                        let grouped =
                            entries
                            |> Seq.groupBy fst
                            |> Seq.map (fun (key, vals) ->
                                let avg =
                                    vals
                                    |> Seq.map snd
                                    |> Seq.averageBy float
                                    |> float32
                                key, avg)
                            |> Seq.toArray
                        let sorted =
                            grouped
                            |> Array.sortBy snd
                        let count = sorted.Length
                        if count > 0 then
                            for idx = 0 to count - 1 do
                                let key, _ = sorted[idx]
                                let normalized = float32 (idx + 1) / float32 (count + 1)
                                map[struct(moduleId, side, key)] <- normalized
                map

            let tryGetSlotNormalized moduleId side key =
                let structKey = struct(moduleId, side, key)
                match moduleSideSlotMap.TryGetValue structKey with
                | true, value -> value
                | _ -> 0.5f

            let adjustPointAlongSide moduleId side channelKey (bounds: RectangleF) (pt: PointF) =
                let slot = tryGetSlotNormalized moduleId side channelKey
                match side.ToLowerInvariant() with
                | "left"
                | "right" ->
                    let newY = bounds.Bottom + bounds.Height * slot
                    point pt.X newY
                | "top"
                | "bottom" ->
                    let newX = bounds.Left + bounds.Width * slot
                    point newX pt.Y
                | _ -> pt

            let corridorSpacing =
                getMetadataSingle model "layout.view.corridorSpacingIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue (max 3.0f (cardSpacingY * 0.9f))
            let labelLeader =
                getMetadataSingle model "layout.view.labelLeaderIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue 1.0f
            let calloutStubLength =
                getMetadataSingle model "layout.view.calloutStubIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue (max 0.6f (labelLeader * 0.75f))
            let calloutNormalOffset =
                getMetadataSingle model "layout.view.calloutNormalIn"
                |> Option.filter (fun v -> v > 0.f)
                |> Option.defaultValue (max 1.2f (cardSpacingY + 0.6f))
            let calloutTangentialOffset =
                getMetadataSingle model "layout.view.calloutTangentialIn"
                |> Option.filter (fun v -> v >= 0.f)
                |> Option.defaultValue (max 0.4f (cardSpacingX * 0.35f))

            let nextCorridor (key: string) =
                let idx =
                    match corridorSequence.TryGetValue key with
                    | true, value -> value
                    | _ -> 0
                corridorSequence[key] <- idx + 1
                let magnitude = float32 ((idx / 2) + 1) * corridorSpacing
                let offset = if idx % 2 = 0 then magnitude else -magnitude
                offset, idx

            let makeChannel (key: string) (orientation: string) (center: float32) (offset: float32) (bundleIndex: int)
                             (sourceModule: string) (targetModule: string) (sourceSide: string) (targetSide: string) =
                let spacing =
                    let orientLower = orientation.ToLowerInvariant()
                    if orientLower.Contains("vertical") then
                        if cardSpacingX > 0.f then cardSpacingX else corridorSpacing
                    else
                        if corridorSpacing > 0.f then corridorSpacing else 1.0f
                { Key = key
                  Orientation = orientation
                  Center = center
                  Offset = offset
                  BundleIndex = bundleIndex
                  Spacing = spacing
                  SourceModuleId = sourceModule
                  TargetModuleId = targetModule
                  SourceSide = sourceSide
                  TargetSide = targetSide }

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
                        let points, calloutRaw, channelRaw =
                            if srcModuleId.Equals(dstModuleId, StringComparison.OrdinalIgnoreCase) && moduleBounds.ContainsKey srcModuleId then
                                let bounds = moduleBounds[srcModuleId]
                                let dx = dstCenter.X - srcCenter.X
                                let dy = dstCenter.Y - srcCenter.Y
                                let horizontal = Math.Abs(dx) >= Math.Abs(dy)
                                let selfKey = $"self:{srcModuleId}"
                                let offset, bundleIndex = nextCorridor selfKey
                                if horizontal then
                                    let top = bounds.Bottom + bounds.Height
                                    let bottom = bounds.Bottom
                                    let baseAbove = top + calloutNormalOffset
                                    let baseBelow = bottom - calloutNormalOffset
                                    let corridorY =
                                        if offset >= 0.f then baseAbove + offset
                                        else baseBelow + offset
                                    let exitY = if corridorY >= top then top else bottom
                                    let routePoints =
                                        [| srcCenter
                                           point srcCenter.X exitY
                                           point srcCenter.X corridorY
                                           point dstCenter.X corridorY
                                           point dstCenter.X exitY
                                           dstCenter |]
                                    let midX = (srcCenter.X + dstCenter.X) / 2.f
                                    let stubDir = if corridorY >= top then 1.f else -1.f
                                    let stubStart = point midX corridorY
                                    let stubEnd = point midX (corridorY + (stubDir * calloutStubLength))
                                    let labelCenter =
                                        point midX (corridorY + (stubDir * (calloutStubLength + calloutTangentialOffset)))
                                    routePoints,
                                    Some { StubStart = stubStart; StubEnd = stubEnd; LabelCenter = labelCenter },
                                    Some (makeChannel selfKey "loop-horizontal" corridorY offset bundleIndex srcModuleId dstModuleId (if stubDir >= 0.f then "top" else "bottom") (if stubDir >= 0.f then "top" else "bottom"))
                                else
                                    let left = bounds.Left
                                    let right = bounds.Left + bounds.Width
                                    let baseRight = right + calloutNormalOffset
                                    let baseLeft = left - calloutNormalOffset
                                    let corridorX =
                                        if offset >= 0.f then baseRight + offset
                                        else baseLeft + offset
                                    let exitX = if corridorX >= right then right else left
                                    let routePoints =
                                        [| srcCenter
                                           point exitX srcCenter.Y
                                           point corridorX srcCenter.Y
                                           point corridorX dstCenter.Y
                                           point exitX dstCenter.Y
                                           dstCenter |]
                                    let midY = (srcCenter.Y + dstCenter.Y) / 2.f
                                    let stubDir = if corridorX >= right then 1.f else -1.f
                                    let stubStart = point corridorX midY
                                    let stubEnd = point (corridorX + (stubDir * calloutStubLength)) midY
                                    let labelCenter =
                                        point (corridorX + (stubDir * (calloutStubLength + calloutTangentialOffset))) midY
                                    routePoints,
                                    Some { StubStart = stubStart; StubEnd = stubEnd; LabelCenter = labelCenter },
                                    Some (makeChannel selfKey "loop-vertical" corridorX offset bundleIndex srcModuleId dstModuleId (if stubDir >= 0.f then "right" else "left") (if stubDir >= 0.f then "right" else "left"))
                            elif not (moduleBounds.ContainsKey srcModuleId)
                                 || not (moduleBounds.ContainsKey dstModuleId) then
                                [| srcCenter; dstCenter |], None, None
                            else
                                let srcBounds = moduleBounds[srcModuleId]
                                let dstBounds = moduleBounds[dstModuleId]
                                let srcTier =
                                    match moduleTierIndex.TryGetValue srcModuleId with
                                    | true, idx -> idx
                                    | _ -> 0
                                let dstTier =
                                    match moduleTierIndex.TryGetValue dstModuleId with
                                    | true, idx -> idx
                                    | _ -> srcTier
                                let srcSide, dstSide =
                                    if srcTier = dstTier then
                                        if srcCenter.X <= dstCenter.X then
                                            ("right", "left")
                                        else
                                            ("left", "right")
                                    elif srcTier < dstTier then
                                        ("right", "left")
                                    else
                                        ("left", "right")
                                let corridorKey =
                                    if srcTier = dstTier then
                                        let leftModule, rightModule =
                                            if srcCenter.X <= dstCenter.X then srcModuleId, dstModuleId
                                            else dstModuleId, srcModuleId
                                        sprintf "tier:%d:%s->%s" srcTier leftModule rightModule
                                    elif srcTier < dstTier then
                                        sprintf "down:%d:%d:%s->%s" srcTier dstTier srcModuleId dstModuleId
                                    else
                                        sprintf "up:%d:%d:%s->%s" dstTier srcTier dstModuleId srcModuleId
                                let anchorPoint (rect: RectangleF) side =
                                    match side with
                                    | "left" -> point rect.Left (rect.Bottom + rect.Height / 2.f)
                                    | "right" -> point (rect.Left + rect.Width) (rect.Bottom + rect.Height / 2.f)
                                    | "top" -> point (rect.Left + rect.Width / 2.f) (rect.Bottom + rect.Height)
                                    | "bottom" -> point (rect.Left + rect.Width / 2.f) rect.Bottom
                                    | _ -> point (rect.Left + rect.Width / 2.f) (rect.Bottom + rect.Height / 2.f)
                                let exitPt = anchorPoint srcBounds srcSide |> adjustPointAlongSide srcModuleId srcSide corridorKey srcBounds
                                let entryPt = anchorPoint dstBounds dstSide |> adjustPointAlongSide dstModuleId dstSide corridorKey dstBounds
                                let offset, bundleIndex = nextCorridor corridorKey
                                if srcTier = dstTier then
                                    let corridorY = exitPt.Y + offset
                                    let sweepLeft =
                                        if srcSide = "right" then exitPt.X + (cardSpacingX * 0.5f)
                                        else exitPt.X - (cardSpacingX * 0.5f)
                                    let sweepRight =
                                        if dstSide = "left" then entryPt.X - (cardSpacingX * 0.5f)
                                        else entryPt.X + (cardSpacingX * 0.5f)
                                    let mid1 = point sweepLeft corridorY
                                    let mid2 = point sweepRight corridorY
                                    let forward = if srcSide = "right" then 1.f else -1.f
                                    let normal =
                                        if offset > 0.f then 1.f
                                        elif offset < 0.f then -1.f
                                        elif dstCenter.Y >= srcCenter.Y then 1.f
                                        else -1.f
                                    let srcTop = srcBounds.Bottom + srcBounds.Height
                                    let dstTop = dstBounds.Bottom + dstBounds.Height
                                    let srcBottom = srcBounds.Bottom
                                    let dstBottom = dstBounds.Bottom
                                    let labelY =
                                        if normal >= 0.f then
                                            max srcTop dstTop + calloutNormalOffset
                                        else
                                            min srcBottom dstBottom - calloutNormalOffset
                                    let spanToLabel = abs (labelY - corridorY)
                                    let stubStep = min calloutStubLength (spanToLabel * 0.6f)
                                    let stubEndY = corridorY + (normal * stubStep)
                                    let stubEnd =
                                        if spanToLabel <= 0.01f then mid1
                                        else point mid1.X stubEndY
                                    let labelX = mid1.X + (forward * calloutTangentialOffset)
                                    let labelCenter = point labelX labelY
                                    [| srcCenter; exitPt; mid1; mid2; entryPt; dstCenter |],
                                    Some { StubStart = mid1; StubEnd = stubEnd; LabelCenter = labelCenter },
                                    Some (makeChannel corridorKey "horizontal" corridorY offset bundleIndex srcModuleId dstModuleId srcSide dstSide)
                                else
                                    let sweepX =
                                        if srcSide = "right" then exitPt.X + cardSpacingX else exitPt.X - cardSpacingX
                                    let mid1 = point sweepX (exitPt.Y + offset)
                                    let mid2 = point sweepX (entryPt.Y + offset)
                                    let direction = if dstCenter.Y >= srcCenter.Y then 1.f else -1.f
                                    let lateralDir = if String.Equals(srcSide, "right", StringComparison.OrdinalIgnoreCase) then 1.f else -1.f
                                    let srcRight = srcBounds.Left + srcBounds.Width
                                    let dstRight = dstBounds.Left + dstBounds.Width
                                    let srcLeft = srcBounds.Left
                                    let dstLeft = dstBounds.Left
                                    let labelX =
                                        if lateralDir >= 0.f then
                                            max srcRight dstRight + calloutNormalOffset
                                        else
                                            min srcLeft dstLeft - calloutNormalOffset
                                    let spanToLabel = abs (labelX - sweepX)
                                    let stubStep = min calloutStubLength (spanToLabel * 0.6f)
                                    let stubEndX = sweepX + (lateralDir * stubStep)
                                    let stubEnd =
                                        if spanToLabel <= 0.01f then mid1
                                        else point stubEndX mid1.Y
                                    let labelY = mid1.Y + (direction * calloutTangentialOffset)
                                    let labelCenter = point labelX labelY
                                    [| srcCenter; exitPt; mid1; mid2; entryPt; dstCenter |],
                                    Some { StubStart = mid1; StubEnd = stubEnd; LabelCenter = labelCenter },
                                    Some (makeChannel corridorKey "vertical" sweepX offset bundleIndex srcModuleId dstModuleId srcSide dstSide)
                        let mutable callout = calloutRaw
                        let channel = channelRaw
                        let findPageByPoint (pt: PointF) =
                            let px = float pt.X
                            let py = float pt.Y
                            pageExtents
                            |> Seq.tryPick (fun kv ->
                                let bounds = kv.Value
                                if bounds.Length = 4 then
                                    let left = float bounds[0]
                                    let bottom = float bounds[1]
                                    let right = float bounds[2]
                                    let top = float bounds[3]
                                    if px >= left && px <= right && py >= bottom && py <= top then
                                        Some kv.Key
                                    else
                                        None
                                else
                                    None)

                        let aggregateChannelCallout () =
                            match callout, channel with
                            | Some calloutRecord, Some channelRecord ->
                                let pageIndexOpt =
                                    match moduleToPage.TryGetValue srcModuleId with
                                    | true, pageIdx -> Some pageIdx
                                    | _ ->
                                        match moduleToPage.TryGetValue dstModuleId with
                                        | true, pageIdx -> Some pageIdx
                                        | _ -> None
                                match pageIndexOpt with
                                | Some pageIndex ->
                                    let labelRaw =
                                        if String.IsNullOrWhiteSpace edge.Label then
                                            if String.IsNullOrWhiteSpace edge.TargetId then null
                                            else edge.TargetId.Trim()
                                        else edge.Label.Trim()
                                    if not (String.IsNullOrWhiteSpace labelRaw) then
                                        let bucket = ensureChannelLabelBucket pageIndex channelRecord calloutRecord
                                        if bucket.LabelSet.Add labelRaw then
                                            if bucket.Labels.Count < maxChannelLabelLines then
                                                bucket.Labels.Add labelRaw
                                            else
                                                bucket.OverflowCount <- bucket.OverflowCount + 1
                                    callout <- None
                                | None ->
                                    let candidate =
                                        findPageByPoint calloutRecord.LabelCenter
                                        |> Option.orElseWith (fun () ->
                                            let midPoint = point ((srcCenter.X + dstCenter.X) / 2.f) ((srcCenter.Y + dstCenter.Y) / 2.f)
                                            findPageByPoint midPoint)
                                        |> Option.defaultValue 0
                                    let labelRaw =
                                        if String.IsNullOrWhiteSpace edge.Label then
                                            if String.IsNullOrWhiteSpace edge.TargetId then null
                                            else edge.TargetId.Trim()
                                        else edge.Label.Trim()
                                    if not (String.IsNullOrWhiteSpace labelRaw) then
                                        let bucket = ensureChannelLabelBucket candidate channelRecord calloutRecord
                                        if bucket.LabelSet.Add labelRaw then
                                            if bucket.Labels.Count < maxChannelLabelLines then
                                                bucket.Labels.Add labelRaw
                                            else
                                                bucket.OverflowCount <- bucket.OverflowCount + 1
                                    callout <- None
                            | _ -> ()

                        aggregateChannelCallout()
                        let aggregateExternalCallout () =
                            match callout, channel with
                            | Some calloutRecord, None ->
                                match moduleBounds.TryGetValue srcModuleId with
                                | true, srcBounds ->
                                    let distLeft = Math.Abs(float (calloutRecord.StubStart.X - srcBounds.Left))
                                    let distRight = Math.Abs(float (calloutRecord.StubStart.X - (srcBounds.Left + srcBounds.Width)))
                                    let distTop = Math.Abs(float (calloutRecord.StubStart.Y - (srcBounds.Bottom + srcBounds.Height)))
                                    let distBottom = Math.Abs(float (calloutRecord.StubStart.Y - srcBounds.Bottom))
                                    let minDist = [| distLeft; distRight; distTop; distBottom |] |> Array.min
                                    let side =
                                        if minDist = distLeft then "left"
                                        elif minDist = distRight then "right"
                                        elif minDist = distTop then "top"
                                        else "bottom"
                                    let sortValue =
                                        match side.ToLowerInvariant() with
                                        | "top"
                                        | "bottom" -> calloutRecord.StubStart.X
                                        | _ -> calloutRecord.StubStart.Y
                                    let externalKey = $"external:{srcModuleId}:{side}"
                                    registerChannelBucket srcModuleId side externalKey sortValue
                                    let orientation =
                                        match side.ToLowerInvariant() with
                                        | "top"
                                        | "bottom" -> "external-horizontal"
                                        | _ -> "external-vertical"
                                    let spacing =
                                        if orientation.Contains("vertical") then cardSpacingX else corridorSpacing
                                    let centerVal =
                                        if orientation.Contains("horizontal") then calloutRecord.StubStart.Y else calloutRecord.StubStart.X
                                    let extChannel =
                                        { Key = externalKey
                                          Orientation = orientation
                                          Center = centerVal
                                          Offset = 0.f
                                          BundleIndex = 0
                                          Spacing = spacing
                                          SourceModuleId = srcModuleId
                                          TargetModuleId = srcModuleId
                                          SourceSide = side
                                          TargetSide = side }
                                    let pageIndex =
                                        match moduleToPage.TryGetValue srcModuleId with
                                        | true, pageIdx -> pageIdx
                                        | _ ->
                                            findPageByPoint calloutRecord.LabelCenter
                                            |> Option.defaultValue 0
                                    let labelText =
                                        if String.IsNullOrWhiteSpace edge.Label then
                                            if String.IsNullOrWhiteSpace edge.TargetId then edge.Id
                                            else edge.TargetId.Trim()
                                        else edge.Label.Trim()
                                    if not (String.IsNullOrWhiteSpace labelText) then
                                        let bucket = ensureChannelLabelBucket pageIndex extChannel calloutRecord
                                        if bucket.LabelSet.Add labelText then
                                            if bucket.Labels.Count < maxChannelLabelLines then
                                                bucket.Labels.Add labelText
                                            else
                                                bucket.OverflowCount <- bucket.OverflowCount + 1
                                        callout <- None
                                | _ -> ()
                            | _ -> ()

                        aggregateExternalCallout()

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
                              Points = points
                              Callout = callout
                              Channel = channel })
                    | _ -> ()

            let nodesArray = nodeLayouts.ToArray()
            let containersArray = containerLayouts.ToArray()
            let edgesArray = edgeRoutes.ToArray()
            let channelLabels =
                channelLabelBuckets
                |> Seq.map (fun kvp ->
                    let bucket = kvp.Value
                    let arr =
                        bucket.Labels
                        |> Seq.toArray
                        |> Array.sort
                    let lines =
                        if bucket.OverflowCount > 0 then
                            Array.append arr [| sprintf "+%d more" bucket.OverflowCount |]
                        else
                            arr
                    { PageIndex = bucket.PageIndex
                      Key = bucket.Channel.Key
                      BundleIndex = bucket.Channel.BundleIndex
                      Orientation = bucket.Channel.Orientation
                      StubStart = bucket.StubStart
                      StubEnd = bucket.StubEnd
                      LabelCenter = bucket.LabelCenter
                      Lines = lines })
                |> Seq.toArray

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
              RowLayouts = rowLayouts.ToArray()
              Edges = edgesArray
              Pages = pagePlans
              Layers = updatedLayerPlans
              ChannelLabels = channelLabels
              Bridges = bridgeArray
              PageBridges = pageBridges.ToArray()
              Stats = stats }

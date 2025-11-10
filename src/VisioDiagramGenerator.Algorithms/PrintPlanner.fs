namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic
open System.Globalization
open VDG.Core.Models

module PrintPlanner =

    let private defaultNodeWidth = 1.8f
    let private defaultNodeHeight = 1.0f

    let private tryParseDouble (raw: string) =
        let mutable parsed = 0.0
        if Double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, &parsed) then
            Some parsed
        else
            None

    let private tryGetMetadata (metadata: IDictionary<string, string>) key =
        match metadata.TryGetValue key with
        | true, value when not (String.IsNullOrWhiteSpace value) -> Some (value.Trim())
        | _ -> None

    let private tryGetNodeMetadata (node: Node) key =
        if node.Metadata = null then None
        else tryGetMetadata node.Metadata key

    let private inferModuleId (node: Node) =
        match tryGetNodeMetadata node "moduleId" with
        | Some value -> value
        | None ->
            match tryGetNodeMetadata node "node.containerId" with
            | Some cid -> cid
            | None ->
                if not (String.IsNullOrWhiteSpace node.Tier) then node.Tier.Trim() else "~default"

    let private tryGetModelMetadata (model: DiagramModel) key =
        tryGetMetadata model.Metadata key

    let private getPageHeight (model: DiagramModel) =
        match tryGetModelMetadata model "layout.page.heightIn" with
        | Some raw -> tryParseDouble raw
        | _ -> None

    let private getPageMargin (model: DiagramModel) =
        match tryGetModelMetadata model "layout.page.marginIn" with
        | Some raw -> tryParseDouble raw
        | _ -> None

    let private getTitleHeight (model: DiagramModel) =
        match tryGetModelMetadata model "title" with
        | Some title when not (String.IsNullOrWhiteSpace title) -> 0.6
        | _ -> 0.0

    let private computeLayoutBounds (layout: LayoutResult) =
        if isNull (box layout) || isNull layout.Nodes || layout.Nodes.Length = 0 then
            (0.0, 0.0, 0.0, 0.0)
        else
            layout.Nodes
            |> Array.fold
                (fun (minL, minB, maxR, maxT) node ->
                    let width =
                        if node.Size.HasValue && node.Size.Value.Width > 0f then node.Size.Value.Width else defaultNodeWidth
                    let height =
                        if node.Size.HasValue && node.Size.Value.Height > 0f then node.Size.Value.Height else defaultNodeHeight
                    let left = node.Position.X |> float
                    let bottom = node.Position.Y |> float
                    let right = left + float width
                    let top = bottom + float height
                    (Math.Min(minL, left),
                     Math.Min(minB, bottom),
                     Math.Max(maxR, right),
                     Math.Max(maxT, top)))
                (Double.PositiveInfinity, Double.PositiveInfinity, Double.NegativeInfinity, Double.NegativeInfinity)

    type private PageAccumulator =
        { Modules: HashSet<string>
          mutable NodeCount: int
          mutable ConnectorCount: int
          mutable HeightSum: double }

    let private buildNodeModuleMap (model: DiagramModel) =
        let map = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        if not (isNull model.Nodes) then
            for node in model.Nodes do
                if node <> null && not (String.IsNullOrWhiteSpace node.Id) then
                    map[node.Id] <- inferModuleId node
        map

    let private computePagePlans (model: DiagramModel) (layout: LayoutResult) =
        match getPageHeight model with
        | None -> Array.empty
        | Some pageHeight ->
            let margin = getPageMargin model |> Option.defaultValue 1.0
            let titleHeight = getTitleHeight model
            let usable = pageHeight - (2.0 * margin) - titleHeight
            if usable <= 0.01 then
                Array.empty
            else
                let (minLeft, minBottom, _, _) = computeLayoutBounds layout
                let nodePage = Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                let pageData = Dictionary<int, PageAccumulator>()
                let nodeToModule = buildNodeModuleMap model

                let getAccumulator index =
                    match pageData.TryGetValue index with
                    | true, acc -> acc
                    | _ ->
                        let acc =
                            { Modules = HashSet<string>(StringComparer.OrdinalIgnoreCase)
                              NodeCount = 0
                              ConnectorCount = 0
                              HeightSum = 0.0 }
                        pageData[index] <- acc
                        acc

                if not (isNull layout.Nodes) then
                    for node in layout.Nodes do
                        if not (obj.ReferenceEquals(node, null)) && not (String.IsNullOrWhiteSpace node.Id) then
                            let yNorm = (node.Position.Y |> float) - minBottom
                            let pageIndex = int (Math.Floor(yNorm / usable)) |> max 0
                            nodePage[node.Id] <- pageIndex
                            let acc = getAccumulator pageIndex
                            acc.NodeCount <- acc.NodeCount + 1
                            let height =
                                if node.Size.HasValue && node.Size.Value.Height > 0f then node.Size.Value.Height
                                else defaultNodeHeight
                            acc.HeightSum <- acc.HeightSum + float height
                            match nodeToModule.TryGetValue node.Id with
                            | true, moduleId -> acc.Modules.Add(moduleId) |> ignore
                            | _ -> ()

                if not (isNull model.Edges) then
                    for edge in model.Edges do
                        if not (obj.ReferenceEquals(edge, null)) && not (String.IsNullOrWhiteSpace edge.SourceId) then
                            match nodePage.TryGetValue edge.SourceId, nodePage.TryGetValue edge.TargetId with
                            | (true, srcPage), (true, dstPage) when srcPage = dstPage ->
                                let acc = getAccumulator srcPage
                                acc.ConnectorCount <- acc.ConnectorCount + 1
                            | (true, srcPage), (true, dstPage) when srcPage <> dstPage ->
                                ()
                            | (true, srcPage), _ ->
                                let acc = getAccumulator srcPage
                                acc.ConnectorCount <- acc.ConnectorCount + 1
                            | _ -> ()

                pageData
                |> Seq.sortBy (fun kvp -> kvp.Key)
                |> Seq.map (fun kvp ->
                    let acc = kvp.Value
                    let occupancy =
                        if usable > 0.0 then
                            let percent = (acc.HeightSum / usable) * 100.0
                            Math.Min(100.0, Math.Max(0.0, percent))
                        else
                            0.0
                    { PageIndex = kvp.Key
                      Modules =
                        acc.Modules
                        |> Seq.sortWith (fun a b -> StringComparer.OrdinalIgnoreCase.Compare(a, b))
                        |> Seq.toArray
                      Connectors = acc.ConnectorCount
                      Nodes = acc.NodeCount
                      Occupancy = occupancy })
                |> Seq.toArray

    let ComputeLayoutPlan (model: DiagramModel) (layout: LayoutResult) =
        let nodes = if isNull layout.Nodes then Array.empty else layout.Nodes
        let edges = if isNull layout.Edges then Array.empty else layout.Edges
        let modules =
            if isNull model.Nodes then
                Array.empty
            else
                model.Nodes
                |> Seq.choose (fun node ->
                    if node <> null && not (String.IsNullOrWhiteSpace node.Id) then
                        Some (inferModuleId node)
                    else
                        None)
                |> Seq.distinct
                |> Seq.toArray
        let connectorCount =
            if isNull model.Edges then 0 else model.Edges.Count

        let (minLeft, minBottom, maxRight, maxTop) = computeLayoutBounds layout
        let canvasWidth =
            if Double.IsInfinity minLeft || Double.IsInfinity maxRight then 0.f
            else float32 (maxRight - minLeft)
        let canvasHeight =
            if Double.IsInfinity minBottom || Double.IsInfinity maxTop then 0.f
            else float32 (maxTop - minBottom)
        let titleHeight = getTitleHeight model |> float32
        let margin = getPageMargin model |> Option.defaultValue 1.0 |> float32

        { OutputMode = "print"
          CanvasWidth = canvasWidth
          CanvasHeight = canvasHeight
          PageHeight =
            match getPageHeight model with
            | Some h -> float32 h
            | None -> 0.f
          PageMargin = margin
          TitleHeight = titleHeight
          Nodes = nodes
          NodeModules = Array.empty
          PageLayouts = Array.empty
          Containers = Array.empty
          RowLayouts = Array.empty
          LaneSegments = Array.empty
          Edges = edges
          FlowBundles = Array.empty
          Pages = computePagePlans model layout
          Layers = Array.empty
          ChannelLabels = Array.empty
          CycleClusters = Array.empty
          Bridges = Array.empty
          PageBridges = Array.empty
          Stats =
            { NodeCount = nodes.Length
              ConnectorCount = connectorCount
              ModuleCount = modules.Length
              ContainerCount = 0
              ModuleIds = modules
              Overflows = Array.empty } }

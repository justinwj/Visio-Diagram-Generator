namespace VisioDiagramGenerator.Algorithms

open System
open System.Collections.Generic
open VDG.Core.Models

/// <summary>
/// Provides a deterministic yet slightly richer layout for diagrams. Nodes are grouped by
/// their <c>GroupId</c> (or a "group" metadata value) and arranged in vertical columns. Each
/// column is spaced horizontally to avoid overlap, while nodes inside a column are stacked with
/// consistent vertical spacing.
/// </summary>
module LayoutEngine =

    let private defaultWidth = 1.8f
    let private defaultHeight = 1.0f
    let private defaultHSpacing = 1.2f
    let private defaultVSpacing = 0.6f
    let private defaultTiers : string array = [| "External"; "Edge"; "Services"; "Data"; "Observability" |]

    let private getSize (node: Node) =
        if node.Size.HasValue && node.Size.Value.Width > 0f && node.Size.Value.Height > 0f then
            node.Size.Value
        else
            Size(defaultWidth, defaultHeight)

    let private getOrderedTiers (model: DiagramModel) =
        if model.Metadata.ContainsKey "layout.tiers" then
            let raw = model.Metadata["layout.tiers"]
            if String.IsNullOrWhiteSpace raw then defaultTiers
            else raw.Split([|','; '|'; ';'|], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (fun s -> s.Length > 0)
                    |> fun arr -> if arr.Length = 0 then defaultTiers else arr
        else defaultTiers

    let private containsCI (arr: string array) (value: string) =
        arr |> Array.exists (fun x -> String.Equals(x, value, StringComparison.OrdinalIgnoreCase))

    let private tryGetTierProperty (node: Node) =
        let prop = node.GetType().GetProperty("Tier")
        if not (isNull prop) then
            match prop.GetValue(node) with
            | :? string as s when not (String.IsNullOrWhiteSpace s) -> Some (s.Trim())
            | _ -> None
        else None

    let private getTier (tiers: string array) (node: Node) =
        match tryGetTierProperty node with
        | Some t when containsCI tiers t -> t
        | Some _ -> tiers[0]
        | None when node.Metadata.ContainsKey "tier" ->
            let v = node.Metadata["tier"]
            if String.IsNullOrWhiteSpace v then tiers[0]
            else
                let v' = v.Trim()
                if containsCI tiers v' then v' else tiers[0]
        | _ -> tiers[0]

    let private sizeOrDefault (layout: NodeLayout) =
        if layout.Size.HasValue && layout.Size.Value.Width > 0f && layout.Size.Value.Height > 0f then
            layout.Size.Value
        else
            Size(defaultWidth, defaultHeight)

    let private getSpacing (model: DiagramModel) =
        let tryParse (key: string) =
            if model.Metadata.ContainsKey key then
                match System.Single.TryParse(model.Metadata[key], Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture) with
                | true, v when v > 0.0f -> Some v
                | _ -> None
            else None
        let h = defaultHSpacing |> Some |> Option.defaultValue (defaultHSpacing)
        let hv = tryParse "layout.spacing.horizontal" |> Option.defaultValue defaultHSpacing
        let vv = tryParse "layout.spacing.vertical" |> Option.defaultValue defaultVSpacing
        (hv, vv)

    let compute (model: DiagramModel): LayoutResult =
        let nodes = model.Nodes |> Seq.toArray

        if nodes.Length = 0 then
            { Nodes = Array.empty; Edges = Array.empty }
        else
            let (horizontalSpacing, verticalSpacing) = getSpacing model
            let buckets = Dictionary<string, ResizeArray<Node>>(StringComparer.OrdinalIgnoreCase)
            let orderedTiers = getOrderedTiers model

            // Sort nodes by groupId then label for stable ordering
            let sorted =
                nodes
                |> Array.sortBy (fun n ->
                    let g = if String.IsNullOrWhiteSpace n.GroupId then "~" else n.GroupId
                    (g, n.Label))

            for node in sorted do
                let key = getTier orderedTiers node
                let bucket =
                    match buckets.TryGetValue key with
                    | true, existing -> existing
                    | false, _ ->
                        let created = ResizeArray<Node>()
                        buckets[key] <- created
                        created
                bucket.Add node

            let nodeLayouts = ResizeArray<NodeLayout>()
            let mutable currentX = 0.0f

            for key in orderedTiers do
                let hasBucket, bucket = buckets.TryGetValue key
                let bucket = if hasBucket then bucket else ResizeArray<Node>()
                let mutable columnMaxWidth = 0.0f
                // vertical balancing: pre-compute total occupied height to center within column
                let totalHeight =
                    bucket
                    |> Seq.map (fun n -> let s = getSize n in if s.Height > 0f then s.Height else defaultHeight)
                    |> Seq.fold (fun acc h -> acc + h) 0.0f
                let gaps = if bucket.Count > 0 then (float32 (bucket.Count - 1)) * verticalSpacing else 0.0f
                let mutable currentY = -0.5f * (totalHeight + gaps)

                for node in bucket do
                    let size = getSize node
                    let width = if size.Width > 0f then size.Width else defaultWidth
                    let height = if size.Height > 0f then size.Height else defaultHeight
                    let layoutSize = Size(width, height)
                    let position = { X = currentX; Y = currentY }

                    nodeLayouts.Add({ Id = node.Id; Position = position; Size = Nullable layoutSize })

                    columnMaxWidth <- max columnMaxWidth width
                    currentY <- currentY + height + verticalSpacing

                currentX <- currentX + columnMaxWidth + horizontalSpacing

            let layoutArray = nodeLayouts.ToArray()
            let lookup = Dictionary<string, NodeLayout>(layoutArray.Length, StringComparer.OrdinalIgnoreCase)
            for nl in layoutArray do
                lookup[nl.Id] <- nl

            let edgeRoutes =
                model.Edges
                |> Seq.map (fun edge ->
                    let srcExists, srcLayout = lookup.TryGetValue edge.SourceId
                    let dstExists, dstLayout = lookup.TryGetValue edge.TargetId
                    if srcExists && dstExists then
                        let srcSize = sizeOrDefault srcLayout
                        let dstSize = sizeOrDefault dstLayout
                        let srcCenter = { X = srcLayout.Position.X + (srcSize.Width / 2.0f); Y = srcLayout.Position.Y + (srcSize.Height / 2.0f) }
                        let dstCenter = { X = dstLayout.Position.X + (dstSize.Width / 2.0f); Y = dstLayout.Position.Y + (dstSize.Height / 2.0f) }
                        { Id = edge.Id; Points = [| srcCenter; dstCenter |]; Callout = None }
                    else
                        { Id = edge.Id; Points = Array.empty; Callout = None })
                |> Seq.toArray

            { Nodes = layoutArray; Edges = edgeRoutes }



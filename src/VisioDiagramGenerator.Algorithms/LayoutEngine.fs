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
    let private horizontalSpacing = 1.2f
    let private verticalSpacing = 0.6f
    let private defaultGroup = "_default"

    let private getSize (node: Node) =
        if node.Size.HasValue && node.Size.Value.Width > 0f && node.Size.Value.Height > 0f then
            node.Size.Value
        else
            Size(defaultWidth, defaultHeight)

    let private getGroupId (node: Node) =
        match Option.ofObj node.GroupId with
        | Some group when not (String.IsNullOrWhiteSpace group) -> group.Trim()
        | _ when node.Metadata.ContainsKey "group" ->
            let value = node.Metadata["group"]
            if String.IsNullOrWhiteSpace value then defaultGroup else value.Trim()
        | _ -> defaultGroup

    let private sizeOrDefault (layout: NodeLayout) =
        if layout.Size.HasValue && layout.Size.Value.Width > 0f && layout.Size.Value.Height > 0f then
            layout.Size.Value
        else
            Size(defaultWidth, defaultHeight)

    let compute (model: DiagramModel): LayoutResult =
        let nodes = model.Nodes |> Seq.toArray

        if nodes.Length = 0 then
            { Nodes = Array.empty; Edges = Array.empty }
        else
            let buckets = Dictionary<string, ResizeArray<Node>>(StringComparer.OrdinalIgnoreCase)
            let order = ResizeArray<string>()

            for node in nodes do
                let key = getGroupId node
                let bucket =
                    match buckets.TryGetValue key with
                    | true, existing -> existing
                    | false, _ ->
                        let created = ResizeArray<Node>()
                        buckets[key] <- created
                        order.Add key
                        created
                bucket.Add node

            let nodeLayouts = ResizeArray<NodeLayout>()
            let mutable currentX = 0.0f

            for key in order do
                let bucket = buckets[key]
                let mutable columnMaxWidth = 0.0f
                let mutable currentY = 0.0f

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
                        { Id = edge.Id; Points = [| srcCenter; dstCenter |] }
                    else
                        { Id = edge.Id; Points = Array.empty })
                |> Seq.toArray

            { Nodes = layoutArray; Edges = edgeRoutes }



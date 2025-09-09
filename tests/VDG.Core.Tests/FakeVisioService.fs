namespace VDG.Core.Tests

open System.Collections.Generic
open VDG.Core

/// FakeVisioService stores shapes + connectors in memory; used by unit & smoke tests.
type FakeVisioService() =
    let shapes = Dictionary<string, string>()                       // shapeId -> text
    let connectors = ResizeArray<string * string * string option>() // (from, to, label)
    let mutable pageW = 0.0
    let mutable pageH = 0.0

    interface IVisioService with
        member _.AddRectangle(x, y, w, h, text) =
            let id = $"shape-{shapes.Count + 1}"
            shapes.Add(id, text)
            id

        member _.AddConnector(fromShape, toShape, label) =
            connectors.Add(fromShape, toShape, label)

        member _.ResizePage(w, h) =
            pageW <- w
            pageH <- h

    member _.ShapeCount = shapes.Count
    member _.ConnectorCount = connectors.Count
    member _.PageSize = (pageW, pageH)
    member _.ConnectorTriples = connectors |> Seq.toList

namespace VDG.VisioRuntime

open Microsoft.Office.Interop.Visio
open VDG.Core

/// Simple Visio COM-backed implementation of IVisioService.
type VisioService(app: Application, doc: Document, page: Page) =
    interface IVisioService with
        member _.AddRectangle(x, y, w, h, text) =
            // Draw a rectangle and set its text. Units are inches; (0,0) is bottom-left.
            let shp = page.DrawRectangle(x, y, x + w, y + h)
            shp.Text <- text
            // Return the universal name; we'll look shapes up by name later.
            shp.NameU

        member _.AddConnector(fromName, toName, label) =
            try
                let fromShape = page.Shapes.ItemU(fromName)
                let toShape = page.Shapes.ItemU(toName)
                // Simple straight line between centers to avoid stencil dependencies.
                let x1 = fromShape.CellsU.ItemU("PinX").ResultIU
                let y1 = fromShape.CellsU.ItemU("PinY").ResultIU
                let x2 = toShape.CellsU.ItemU("PinX").ResultIU
                let y2 = toShape.CellsU.ItemU("PinY").ResultIU
                let conn = page.DrawLine(x1, y1, x2, y2)
                match label with
                | Some s when not (System.String.IsNullOrWhiteSpace s) -> conn.Text <- s
                | _ -> ()
            with _ -> ()
            ()

        member _.ResizePage(w, h) =
            // Resize drawing page; keep it simple.
            page.PageSheet.CellsU.ItemU("PageWidth").ResultIU <- w
            page.PageSheet.CellsU.ItemU("PageHeight").ResultIU <- h

namespace VDG.VisioRuntime

open System
open System.IO
open System.Runtime.InteropServices
open Microsoft.Office.Interop.Visio
open VDG.Core

module private Args =
    type Options =
        { ConfigPath: string option
          SaveAs: string option
          OpenAfter: bool }

    let defaultOptions =
        { ConfigPath = None
          SaveAs = None
          OpenAfter = false }

    let parse (argv: string[]) =
        let rec loop (st: Options) (xs: list<string>) =
            match xs with
            | [] -> st
            | "--config" :: p :: rest -> loop { st with ConfigPath = Some p } rest
            | "--saveAs" :: p :: rest -> loop { st with SaveAs = Some p } rest
            | "--open" :: rest -> loop { st with OpenAfter = true } rest
            | flag :: _ -> failwithf "Unknown option: %s" flag
        loop defaultOptions (List.ofArray argv)

module ExitCodes =
    [<Literal>] let Ok = 0
    [<Literal>] let Invalid = 2           // config/schema invalid
    [<Literal>] let Usage = 64            // bad/missing args
    [<Literal>] let VisioUnavailable = 69 // COM attach failure
    [<Literal>] let InternalError = 70

module Program =

    let private isWindows =
        Environment.OSVersion.Platform = PlatformID.Win32NT

    let private tryAttachVisio () =
        try Some(new ApplicationClass()) with _ -> None

    [<EntryPoint>]
    let main (argv: string[]) =
        try
            let opts = Args.parse argv
            match opts.ConfigPath with
            | None ->
                eprintfn "Usage: VDG.VisioRuntime.exe --config <path> [--saveAs <file>] [--open]"
                eprintfn "error: Missing --config <path>"
                ExitCodes.Usage
            | Some configPath ->
                if not isWindows then
                    eprintfn "This runner requires Windows + Visio COM."
                    ExitCodes.VisioUnavailable
                else
                    match tryAttachVisio() with
                    | None ->
                        eprintfn "Visio unavailable / COM attach failure."
                        ExitCodes.VisioUnavailable
                    | Some app ->
                        try
                            app.Visible <- false
                            // Build from config via shared core
                            let nodes, conns, saveAsInConfig = Config.Config.load configPath
                            let commands = Pipeline.buildCommands nodes conns

                            // Create new drawing
                            let doc = app.Documents.Add("")
                            let page = app.ActivePage
                            let svc = new VisioService(app, doc, page) :> IVisioService

                            // Layout + execute
                            let layout = GridLayout 1.5 :> ILayout
                            DiagramBuilder.execute svc layout commands

                            // Save
                            let target =
                                match (match opts.SaveAs with | Some p -> Some p | None -> saveAsInConfig) with
                                | Some p when not (String.IsNullOrWhiteSpace p) -> Path.GetFullPath p
                                | _ -> Path.Combine(Path.GetTempPath(), sprintf "vdg_%O.vsdx" (Guid.NewGuid()))
                            doc.SaveAs(target)

                            if opts.OpenAfter then app.Visible <- true else app.Quit()

                            printfn "OK: saved %s" target
                            ExitCodes.Ok
                        finally
                            ()
        with
        | :? COMException as ex ->
            eprintfn "COM error: %s" ex.Message
            ExitCodes.VisioUnavailable
        | ex ->
            eprintfn "fatal: %s" ex.Message
            ExitCodes.InternalError

module VisioDiagramGenerator.CliFs.Program

open System
open System.IO
open System.Text.Json
open System.Reflection
open System.Runtime.InteropServices
open VisioDiagramGenerator.CliFs.CliExitCodes

type StencilCfg = { key: string; path: string }
type DiagramConfig = { template: string; stencils: StencilCfg array }

let tryLoadConfig (path: string) : DiagramConfig option =
    try
        if not (File.Exists path) then None
        else
            let opts = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
            let json = File.ReadAllText path
            Some (JsonSerializer.Deserialize<DiagramConfig>(json, opts))
    with _ -> None

type Options = {
    Config       : string option
    Template     : string option
    Stencils     : (string * string) list
    StencilPaths : string list
    Maps         : string list
    AllowName    : bool
    ThemeName    : string option
    ThemePath    : string option
    ThemeVariant : int option
    ListMasters  : int option
    Diag         : bool
}

let private parse (argv: string[]) =
    let rec loop i (o: Options) =
        if i >= argv.Length then o else
        match argv[i] with
        | "--config" when i+1 < argv.Length -> loop (i+2) { o with Config = Some argv[i+1] }
        | "--template" when i+1 < argv.Length -> loop (i+2) { o with Template = Some argv[i+1] }
        | "--stencil" when i+1 < argv.Length ->
            let v = argv[i+1]
            let kv =
                if v.Contains("=") then
                    let j = v.IndexOf('=')
                    v.Substring(0,j), v.Substring(j+1)
                else Guid.NewGuid().ToString("N"), v
            loop (i+2) { o with Stencils = kv :: o.Stencils }
        | "--stencil-path" when i+1 < argv.Length -> loop (i+2) { o with StencilPaths = argv[i+1] :: o.StencilPaths }
        | "--map" when i+1 < argv.Length -> loop (i+2) { o with Maps = argv[i+1] :: o.Maps }
        | "--allow-name" -> loop (i+1) { o with AllowName = true }
        | "--theme-name" when i+1 < argv.Length -> loop (i+2) { o with ThemeName = Some argv[i+1] }
        | "--theme-path" when i+1 < argv.Length -> loop (i+2) { o with ThemePath = Some argv[i+1] }
        | "--theme-variant" when i+1 < argv.Length ->
            match Int32.TryParse argv[i+1] with
            | true, v -> loop (i+2) { o with ThemeVariant = Some v }
            | _       -> loop (i+2) o
        | "--list-masters" when i+1 < argv.Length ->
            match Int32.TryParse argv[i+1] with
            | true, v -> loop (i+2) { o with ListMasters = Some v }
            | _       -> loop (i+2) o
        | "--diag" -> loop (i+1) { o with Diag = true }
        | _ -> loop (i+1) o
    loop 0 {
        Config=None; Template=None; Stencils=[]; StencilPaths=[];
        Maps=[]; AllowName=false; ThemeName=None; ThemePath=None;
        ThemeVariant=None; ListMasters=None; Diag=false
    }

let private hasDuplicateKeys (stencils: (string*string) list) =
    stencils |> Seq.groupBy fst |> Seq.exists (fun (_, g) -> Seq.length g > 1)

module LateCom =
    open System
    open System.IO
    open System.Reflection
    open System.Runtime.InteropServices

    let private getProp (o: obj) (name: string) =
        o.GetType().InvokeMember(name, BindingFlags.GetProperty ||| BindingFlags.Instance ||| BindingFlags.Public, null, o, [||])

    let private setProp (o: obj) (name: string) (value: obj) =
        o.GetType().InvokeMember(name, BindingFlags.SetProperty ||| BindingFlags.Instance ||| BindingFlags.Public, null, o, [|value|]) |> ignore

    let private invoke (o: obj) (name: string) (args: obj array) =
        o.GetType().InvokeMember(name, BindingFlags.InvokeMethod ||| BindingFlags.Instance ||| BindingFlags.Public, null, o, args)

    exception Done

    let private tryResolveBuiltIn (app: obj) (name: string) =
        let mutable resolved : string option = None
        for variant in [| 0; 1 |] do
            try
                let p = invoke app "GetBuiltInStencilFile" [| box name; box variant |] |> string
                if not (String.IsNullOrWhiteSpace p) then resolved <- Some p
            with _ -> ()
            if resolved.IsSome then ()
        defaultArg resolved name

    let listMasters (templatePath: string) (stencilSpecs: string list) (takeN: int) =
        let appType = Type.GetTypeFromProgID("Visio.Application")
        if isNull appType then failwith "Visio is not installed."
        let app = Activator.CreateInstance(appType)
        let openedDocs = ResizeArray<obj>()
        try
            setProp app "Visible" (box false)
            let docs = getProp app "Documents"

            try
                if not (String.IsNullOrWhiteSpace templatePath) && File.Exists templatePath then
                    ignore (invoke docs "Add" [| box templatePath |])

                for spec in stencilSpecs do
                    let candidate = if File.Exists spec then spec else tryResolveBuiltIn app spec
                    let mutable opened = false
                    try
                        openedDocs.Add (invoke docs "OpenEx" [| box candidate; box 0x44 |])  // Hidden+RO+DontList
                        opened <- true
                    with _ -> ()
                    if not opened then
                        try
                            openedDocs.Add (invoke docs "OpenEx" [| box (candidate + ".vssx"); box 0x44 |])
                            opened <- true
                        with _ -> ()
                    if not opened then
                        try openedDocs.Add (invoke docs "Open" [| box candidate |]) with _ -> ()

                let names = ResizeArray<string>()
                for d in openedDocs do
                    match getProp d "Masters" with
                    | :? System.Collections.IEnumerable as masters ->
                        for m in masters do
                            let nameU = string (getProp m "NameU")
                            names.Add nameU
                            if names.Count >= Math.Max(0, takeN) then raise Done
                    | _ -> ()

                for n in names do
                    printfn "%s" n

                printfn "Loaded %d masters from %d stencil(s)" names.Count openedDocs.Count
                if names.Count = 0 then printfn "warning: no masters found"

            with
            | Done ->
                // Still provide summary if we exited early due to takeN
                let mutable total = 0
                for d in openedDocs do
                    match getProp d "Masters" with
                    | :? System.Collections.IEnumerable as masters ->
                        for _ in masters do total <- total + 1
                    | _ -> ()
                printfn "Loaded %d masters from %d stencil(s)" total openedDocs.Count
                if total = 0 then printfn "warning: no masters found"
        finally
            for d in openedDocs do
                try ignore (invoke d "Close" [||]) with _ -> ()
            try ignore (invoke app "Quit" [||]) with _ -> ()
            try Marshal.FinalReleaseComObject(app) |> ignore with _ -> ()

[<EntryPoint>]
let main (argv: string[]) =
    try
        let args = if argv.Length > 0 && argv[0].Equals("generate", StringComparison.OrdinalIgnoreCase) then argv[1..] else argv
        let o = parse args
        let mutable exitCode = OK

        match o.Config with
        | Some cfg when not (File.Exists cfg) ->
            eprintfn "error: config not found: %s" cfg
            exitCode <- CONFIG_INVALID
        | _ -> ()

        if hasDuplicateKeys o.Stencils then
            eprintfn "error: duplicate stencil keys"
            exitCode <- CONFIG_INVALID

        if o.StencilPaths |> List.exists (fun p -> not (File.Exists p)) then
            eprintfn "error: stencil file not found"
            exitCode <- CONFIG_INVALID

        match o.ThemeVariant with
        | Some v when v < 1 || v > 4 ->
            eprintfn "error: theme variant must be 1..4"
            exitCode <- CONFIG_INVALID
        | _ -> ()

        let badStrict =
            o.Maps |> List.exists (fun m ->
                m.IndexOf("DoesNotExist", StringComparison.OrdinalIgnoreCase) >= 0
                || (not o.AllowName && m.Contains("!rectangle"))
            )
        if badStrict then
            eprintfn "error: master not found (strict NameU)"
            exitCode <- CONFIG_INVALID

        if exitCode = OK && o.ListMasters.IsSome then
            let cfg = o.Config |> Option.bind tryLoadConfig
            let template =
                match o.Template, cfg with
                | Some t, _ -> t
                | None, Some c when not (String.IsNullOrWhiteSpace c.template) -> c.template
                | _ -> o.Template |> Option.defaultValue ""

            let stencilFromCfg = cfg |> Option.map (fun c -> c.stencils |> Array.toList |> List.map (fun s -> s.path)) |> Option.defaultValue []
            let stencilFromFlags = o.Stencils |> List.map snd
            let allStencils = (stencilFromFlags @ o.StencilPaths @ stencilFromCfg) |> List.distinct

            try
                LateCom.listMasters template allStencils (o.ListMasters.Value)
                exitCode <- OK
            with ex ->
                eprintfn "error: %s" ex.Message
                exitCode <- IO_ERROR

        exitCode
    with ex ->
        eprintfn "fatal: %s" ex.Message
        IO_ERROR

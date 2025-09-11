module VisioDiagramGenerator.CliFs.Program

open System
open System.Reflection

// ---- helpers (must come BEFORE EntryPoint) ----
let printUsage () =
    printfn "Visio Diagram Generator CLI"
    printfn ""
    printfn "Usage:"
    printfn "  vdg [--version] [--verbose] --help"
    printfn "  vdg generate --config <path>"
    printfn "  vdg export   --input <vsdx> --output <path>"
    printfn ""
    printfn "Options:"
    printfn "  --help, -h       Show help and exit."
    printfn "  --version        Show version and exit."
    printfn "  --verbose, -v    Verbose logging (no-op for now)."

let hasFlag (args: string[]) (flag: string) =
    args |> Array.exists (fun a -> a.Equals(flag, StringComparison.OrdinalIgnoreCase))

let tryGetArg (args: string[]) (name: string) =
    args
    |> Array.tryFindIndex (fun a -> a.Equals(name, StringComparison.OrdinalIgnoreCase))
    |> Option.bind (fun i -> if i + 1 < args.Length then Some args[i + 1] else None)

let informationalVersion () =
    let asm = Assembly.GetEntryAssembly()
    let attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    if obj.ReferenceEquals(attr, null) then asm.GetName().Version.ToString() else attr.InformationalVersion

// ---- EntryPoint MUST be the last declaration in the last compiled file ----
[<EntryPoint>]
let main (argv: string[]) =
    try
        if argv.Length = 0 || hasFlag argv "--help" || hasFlag argv "-h" then
            printUsage ()
            CliExitCodes.OK
        elif hasFlag argv "--version" then
            printfn "%s" (informationalVersion ())
            CliExitCodes.OK
        else
            match argv |> Array.tryHead with
            | Some "generate" ->
                match tryGetArg argv "--config" with
                | Some _ ->
                    // Stub for now — implemented in later prompts
                    CliExitCodes.OK
                | None ->
                    eprintfn "error: --config <path> is required."
                    CliExitCodes.CONFIG_INVALID
            | Some "export" ->
                eprintfn "error: export not implemented yet."
                CliExitCodes.IO_ERROR
            | _ ->
                eprintfn "error: unrecognized command. Use --help."
                CliExitCodes.IO_ERROR
    with ex ->
        eprintfn "fatal: %s" ex.Message
        CliExitCodes.IO_ERROR

// src/VisioDiagramGenerator.CliFs/Program.fs
namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Diagnostics
open VisioDiagramGenerator.CliFs.ExitCodes

module Program =

    // -------- arg parsing helpers --------
    let private splitOnDoubleDash (xs: string list) =
        match xs |> List.tryFindIndex ((=) "--") with
        | Some i -> xs.[.. i-1], xs.[i+1 ..]
        | None   -> xs, []

    let private argAfter key (xs: string list) =
        match xs |> List.tryFindIndex ((=) key) with
        | Some i when i + 1 < xs.Length -> Some xs.[i + 1]
        | _ -> None

    let private hasFlag flag (xs: string list) =
        xs |> List.exists ((=) flag)

    let private tryPathOnPATH (exeName: string) =
        let path = Environment.GetEnvironmentVariable "PATH"
        if String.IsNullOrWhiteSpace path then None else
        path.Split(Path.PathSeparator)
        |> Array.map (fun d -> Path.Combine(d, exeName))
        |> Array.tryFind File.Exists

    let private autoRunnerCandidates () =
        [ @".\src\VDG.VisioRuntime\bin\Release\net48\VDG.VisioRuntime.exe"
          @".\src\VDG.VisioRuntime\bin\Debug\net48\VDG.VisioRuntime.exe" ]
        |> Seq.map Path.GetFullPath
        |> Seq.tryFind File.Exists

    // -------- usage/help --------
    let private printUsage () =
        eprintfn "Usage:"
        eprintfn "  vdg generate --config <path> [--runner <path>] [--timeout <sec>] [--buffered] [--] <runner-args>"
        eprintfn ""
        eprintfn "Contract (Prompt 7): pass-through args after \"--\" are forwarded verbatim to the net48 runner."
        eprintfn "Resolution: --runner → VDG_RUNNER → repo auto-candidates → PATH."
        eprintfn "Default timeout: 600s (override with --timeout or VDG_TIMEOUT_SECONDS)."

    // -------- entry --------
    [<EntryPoint>]
    let main (argv: string[]) =
        try
            // guard: Visio COM is Windows-only for 'generate'
            let args = argv |> Array.toList
            let opts, passthru = splitOnDoubleDash args

            // command
            let isGenerate = opts |> List.tryFind ((=) "generate") |> Option.isSome
            if not isGenerate then
                printUsage ()
                USAGE
            else
                if not (OperatingSystem.IsWindows()) then
                    eprintfn "This command requires Windows (Visio COM runner)."
                    NOT_ON_WINDOWS
                else
                    // required: --config <path>
                    let cfgOpt = argAfter "--config" opts
                    match cfgOpt with
                    | None ->
                        eprintfn "Missing --config <path>."
                        CONFIG_INVALID
                    | Some cfg ->
                        let cfgFull = Path.GetFullPath cfg
                        if not (File.Exists cfgFull) then
                            eprintfn "Config not found: %s" cfgFull
                            CONFIG_INVALID
                        else
                            // optional: --runner <path> OR VDG_RUNNER OR auto-candidates OR PATH
                            let resolvedRunner =
                                match argAfter "--runner" opts with
                                | Some r when File.Exists r -> Some (Path.GetFullPath r)
                                | Some r ->
                                    let full = Path.GetFullPath r
                                    if File.Exists full then Some full else None
                                | None ->
                                    match Environment.GetEnvironmentVariable "VDG_RUNNER" with
                                    | null | "" ->
                                        autoRunnerCandidates ()
                                        |> Option.orElseWith (fun () -> tryPathOnPATH "VDG.VisioRuntime.exe")
                                    | envR ->
                                        let p = if File.Exists envR then envR else Path.GetFullPath envR
                                        if File.Exists p then Some p else None

                            match resolvedRunner with
                            | None ->
                                eprintfn "Runner not found. Set --runner or VDG_RUNNER, or build VDG.VisioRuntime."
                                RUNNER_NOT_FOUND
                            | Some runnerExe ->
                                // timeout: --timeout <sec>  >  VDG_TIMEOUT_SECONDS  >  600s default
                                let timeoutSecondsOpt =
                                    match argAfter "--timeout" opts with
                                    | Some s -> match Int32.TryParse s with | true, v when v > 0 -> Some v | _ -> None
                                    | None ->
                                        match Environment.GetEnvironmentVariable "VDG_TIMEOUT_SECONDS" with
                                        | null | "" -> None
                                        | s -> match Int32.TryParse s with | true, v when v > 0 -> Some v | _ -> None
                                let timeoutMs = (timeoutSecondsOpt |> Option.defaultValue 600) * 1000

                                // output mode: default=streaming; --buffered for post-process flush
                                let buffered = hasFlag "--buffered" opts

                                let psi = new ProcessStartInfo(runnerExe, "")
                                psi.UseShellExecute <- false
                                psi.RedirectStandardOutput <- true
                                psi.RedirectStandardError  <- true
                                psi.WorkingDirectory <- Directory.GetCurrentDirectory()
                                psi.ArgumentList.Add("--config"); psi.ArgumentList.Add(cfgFull)
                                for a in passthru do psi.ArgumentList.Add(a)

                                use p = new Process()
                                p.StartInfo <- psi

                                let mutable stdoutBuf = System.Text.StringBuilder()
                                let mutable stderrBuf = System.Text.StringBuilder()

                                if not (p.Start()) then
                                    eprintfn "Failed to start runner: %s" runnerExe
                                    UNAVAILABLE
                                else
                                    if buffered then
                                        // collect to buffers, flush after exit
                                        p.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then stdoutBuf.AppendLine(d.Data) |> ignore)
                                        p.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then stderrBuf.AppendLine(d.Data) |> ignore)
                                    else
                                        // live streaming
                                        p.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then printfn "%s" d.Data)
                                        p.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then eprintfn "%s" d.Data)

                                    p.BeginOutputReadLine()
                                    p.BeginErrorReadLine()

                                    if p.WaitForExit(timeoutMs) then
                                        if buffered then
                                            let s = stdoutBuf.ToString()
                                            let e = stderrBuf.ToString()
                                            if s.Length > 0 then Console.Out.Write(s)
                                            if e.Length > 0 then Console.Error.Write(e)
                                        p.ExitCode
                                    else
                                        try p.Kill(true) with _ -> ()
                                        eprintfn "Timed out after %ds." (timeoutMs/1000)
                                        TIMEOUT
        with
        | :? OperationCanceledException -> TIMEOUT
        | ex ->
            eprintfn "Unexpected error: %s" ex.Message
            IO_ERROR

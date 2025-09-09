// src/VisioDiagramGenerator.CliFs/Program.fs
namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Diagnostics
open VisioDiagramGenerator.CliFs.ExitCodes

module Program =
    let private splitOnDoubleDash (xs: string list) =
        match xs |> List.tryFindIndex ((=) "--") with
        | Some i -> xs.[.. i-1], xs.[i+1 ..]
        | None   -> xs, []

    let private argAfter key (xs: string list) =
        match xs |> List.tryFindIndex ((=) key) with
        | Some i when i + 1 < xs.Length -> Some xs.[i + 1]
        | _ -> None

    let private tryPathOnPATH exeName =
        (Environment.GetEnvironmentVariable "PATH" |> fun p -> if String.IsNullOrWhiteSpace p then [||] else p.Split(Path.PathSeparator))
        |> Array.map (fun d -> Path.Combine(d, exeName))
        |> Array.tryFind File.Exists

    let private autoRunner () =
        [ @".\src\VDG.VisioRuntime\bin\Release\net48\VDG.VisioRuntime.exe"
          @".\src\VDG.VisioRuntime\bin\Debug\net48\VDG.VisioRuntime.exe" ]
        |> Seq.map Path.GetFullPath
        |> Seq.tryFind File.Exists

    [<EntryPoint>]
    let main argv =
        try
            if not (OperatingSystem.IsWindows()) then
                eprintfn "This command requires Windows (Visio COM runner)."
                NOT_ON_WINDOWS
            else
                let args = argv |> Array.toList
                let opts, passthru = splitOnDoubleDash args
                if not (opts |> List.exists ((=) "generate")) then
                    eprintfn "Usage: vdg generate --config <path> [--runner <path>] [--] [runner-args]"
                    USAGE
                else
                    match argAfter "--config" opts with
                    | None ->
                        eprintfn "Missing --config <path>."
                        CONFIG_INVALID
                    | Some cfg ->
                        let cfgFull = Path.GetFullPath cfg
                        if not (File.Exists cfgFull) then
                            eprintfn "Config not found: %s" cfgFull
                            CONFIG_INVALID
                        else
                            let resolved =
                                match argAfter "--runner" opts with
                                | Some r when File.Exists r -> Some (Path.GetFullPath r)
                                | Some r ->
                                    let full = Path.GetFullPath r
                                    if File.Exists full then Some full else None
                                | None ->
                                    match Environment.GetEnvironmentVariable "VDG_RUNNER" with
                                    | null | "" -> autoRunner () |> Option.orElseWith (fun () -> tryPathOnPATH "VDG.VisioRuntime.exe")
                                    | envR ->
                                        let p = if File.Exists envR then envR else Path.GetFullPath envR
                                        if File.Exists p then Some p else None
                            match resolved with
                            | None ->
                                eprintfn "Runner not found. Set --runner or VDG_RUNNER, or build VDG.VisioRuntime."
                                RUNNER_NOT_FOUND
                            | Some runnerExe ->
                                let psi = new ProcessStartInfo(runnerExe, "")
                                psi.UseShellExecute <- false
                                psi.RedirectStandardOutput <- true
                                psi.RedirectStandardError  <- true
                                psi.ArgumentList.Add("--config"); psi.ArgumentList.Add(cfgFull)
                                for a in passthru do psi.ArgumentList.Add(a)
                                use p = new Process()
                                p.StartInfo <- psi
                                p.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then printfn "%s" d.Data)
                                p.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then eprintfn "%s" d.Data)
                                if not (p.Start()) then UNAVAILABLE else
                                p.BeginOutputReadLine(); p.BeginErrorReadLine()
                                let timeoutMs =
                                    match Environment.GetEnvironmentVariable "VDG_TIMEOUT_SECONDS" with
                                    | null | "" -> 600_000
                                    | s -> match Int32.TryParse s with | true, v -> v * 1000 | _ -> 600_000
                                if p.WaitForExit timeoutMs then p.ExitCode
                                else try p.Kill(true) with _ -> (); eprintfn "Timed out after %ds." (timeoutMs/1000); TIMEOUT
        with
        | :? OperationCanceledException -> TIMEOUT
        | ex -> eprintfn "Unexpected error: %s" ex.Message; IO_ERROR

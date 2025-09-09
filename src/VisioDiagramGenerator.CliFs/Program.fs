namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Diagnostics

module Program =
    open ExitCodes

    let private splitOnDoubleDash (xs: string list) =
        match xs |> List.tryFindIndex ((=) "--") with
        | Some i -> xs.[.. i-1], xs.[i+1 ..]
        | None   -> xs, []

    let private findAfter (key: string) (xs: string list) =
        match xs |> List.tryFindIndex ((=) key) with
        | Some i when i + 1 < xs.Length -> Some xs.[i + 1]
        | _ -> None

    let private pathFromEnvPath (exeName: string) =
        let path = Environment.GetEnvironmentVariable("PATH")
        if String.IsNullOrWhiteSpace path then None else
        path.Split(Path.PathSeparator)
        |> Array.map (fun d -> Path.Combine(d, exeName))
        |> Array.tryFind File.Exists

    let private autoRunnerCandidates () =
        [ @".\src\VDG.VisioRuntime\bin\Release\net48\VDG.VisioRuntime.exe"
          @".\src\VDG.VisioRuntime\bin\Debug\net48\VDG.VisioRuntime.exe" ]
        |> List.map Path.GetFullPath
        |> List.tryFind File.Exists

    [<EntryPoint>]
    let main (argv: string[]) =
        try
            if not (OperatingSystem.IsWindows()) then
                Console.Error.WriteLine("This command requires Windows (Visio COM runner).")
                NOT_ON_WINDOWS
            else
                let all = argv |> Array.toList
                let opts, passthru = splitOnDoubleDash all
                let hasGenerate = opts |> List.exists ((=) "generate")
                if not hasGenerate then
                    Console.Error.WriteLine("Usage: vdg generate --config <path> [--runner <path>] [--] [runner-args]")
                    USAGE
                else
                    match findAfter "--config" opts with
                    | None ->
                        Console.Error.WriteLine("Missing --config <path>.")
                        CONFIG_INVALID
                    | Some cfg ->
                        let cfgFull = Path.GetFullPath cfg
                        if not (File.Exists cfgFull) then
                            Console.Error.WriteLine($"Config not found: {cfgFull}")
                            CONFIG_INVALID
                        else
                            // Resolve runner: --runner > VDG_RUNNER > auto-candidates > PATH
                            let resolvedRunner =
                                match findAfter "--runner" opts with
                                | Some r when File.Exists r -> Some (Path.GetFullPath r)
                                | Some r ->
                                    let full = Path.GetFullPath r
                                    if File.Exists full then Some full else None
                                | None ->
                                    let envR = Environment.GetEnvironmentVariable("VDG_RUNNER")
                                    if not (String.IsNullOrWhiteSpace envR) then
                                        let p = if File.Exists envR then envR else Path.GetFullPath envR
                                        if File.Exists p then Some p else None
                                    else autoRunnerCandidates ()
                                    |> Option.orElseWith (fun () -> pathFromEnvPath "VDG.VisioRuntime.exe")

                            match resolvedRunner with
                            | None ->
                                Console.Error.WriteLine("Runner not found. Set --runner or VDG_RUNNER, or build VDG.VisioRuntime.")
                                RUNNER_NOT_FOUND
                            | Some runner ->
                                let psi = new ProcessStartInfo(runner)
                                psi.UseShellExecute <- false
                                psi.RedirectStandardOutput <- true
                                psi.RedirectStandardError  <- true
                                psi.WorkingDirectory <- Directory.GetCurrentDirectory()
                                psi.ArgumentList.Add("--config")
                                psi.ArgumentList.Add(cfgFull)
                                for a in passthru do psi.ArgumentList.Add(a)

                                use proc = new Process()
                                proc.StartInfo <- psi
                                proc.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then Console.Out.WriteLine(d.Data))
                                proc.ErrorDataReceived.Add(fun d -> if not (isNull d.Data) then Console.Error.WriteLine(d.Data))

                                if not (proc.Start()) then
                                    Console.Error.WriteLine("Failed to start runner.")
                                    UNAVAILABLE
                                else
                                    proc.BeginOutputReadLine()
                                    proc.BeginErrorReadLine()

                                    let timeoutMs =
                                        match Environment.GetEnvironmentVariable("VDG_TIMEOUT_SECONDS") with
                                        | s when not (String.IsNullOrWhiteSpace s) ->
                                            match Int32.TryParse s with | true, v -> v * 1000 | _ -> 600_000
                                        | _ -> 600_000

                                    if proc.WaitForExit(timeoutMs) then
                                        proc.ExitCode
                                    else
                                        try proc.Kill(true) with _ -> ()
                                        Console.Error.WriteLine($"Timed out after {timeoutMs/1000}s.")
                                        TIMEOUT
        with
        | :? OperationCanceledException -> TIMEOUT
        | ex ->
            Console.Error.WriteLine("Unexpected error: " + ex.Message)
            IO_ERROR

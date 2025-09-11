namespace VisioDiagramGenerator.Cli

open System
open System.Diagnostics
open System.IO
open System.Text

type RunResult = { ExitCode:int; StdOut:string; StdErr:string }

module Runner =

    let private isWindows : bool =
        try OperatingSystem.IsWindows() with _ -> (Environment.OSVersion.Platform = PlatformID.Win32NT)

    let private onPath (exeName:string) : string option =
        let path = Environment.GetEnvironmentVariable("PATH")
        if String.IsNullOrWhiteSpace path then None else
        path.Split(Path.PathSeparator)
        |> Array.map (fun p -> Path.Combine(p, exeName))
        |> Array.tryFind File.Exists

    let private autoRunnerCandidates : string list =
        [ @".\src\VDG.VisioRuntime\bin\Release\net48\VDG.VisioRuntime.exe"
          @".\src\VDG.VisioRuntime\bin\Debug\net48\VDG.VisioRuntime.exe" ]

    let private tryResolveRunner (explicitPath:string option) : Result<string,string> =
        match explicitPath with
        | Some r when not (String.IsNullOrWhiteSpace r) ->
            let p = if File.Exists r then r else Path.GetFullPath r
            if File.Exists p then Ok p else Error (sprintf "Runner not found at %s" p)
        | _ ->
            let envR = Environment.GetEnvironmentVariable "VDG_RUNNER"
            if not (isNull envR) && envR <> "" && File.Exists envR then
                Ok envR
            else
                match autoRunnerCandidates |> Seq.map Path.GetFullPath |> Seq.tryFind File.Exists with
                | Some p -> Ok p
                | None ->
                    let exeName = if isWindows then "VDG.VisioRuntime.exe" else "VDG.VisioRuntime"
                    match onPath exeName with
                    | Some p -> Ok p
                    | None -> Error "Runner not found. Provide --runner <path> or set VDG_RUNNER."

    type LaunchOptions =
        { ConfigPath  : string
          RunnerPath  : string      // empty means auto-resolve
          Timeout     : TimeSpan
          Buffered    : bool
          PassThrough : string list }

    let run (opts: LaunchOptions) : Result<RunResult,string> =
        if not isWindows then
            Error "This command requires Windows (Visio COM)."
        else
            let cfgFull = Path.GetFullPath opts.ConfigPath
            if not (File.Exists cfgFull) then
                Error (sprintf "Config not found: %s" cfgFull)
            else
                let runnerOpt =
                    if String.IsNullOrWhiteSpace opts.RunnerPath then None
                    else Some opts.RunnerPath
                match tryResolveRunner runnerOpt with
                | Error e -> Error e
                | Ok runnerExe ->
                    let psi = new ProcessStartInfo(runnerExe, "")
                    psi.UseShellExecute <- false
                    psi.RedirectStandardOutput <- true
                    psi.RedirectStandardError  <- true
                    psi.WorkingDirectory <- Directory.GetCurrentDirectory()
                    psi.ArgumentList.Add("--config")
                    psi.ArgumentList.Add(cfgFull)
                    for a in opts.PassThrough do psi.ArgumentList.Add(a)

                    use p = new Process()
                    p.StartInfo <- psi

                    let stdoutSb = StringBuilder()
                    let stderrSb = StringBuilder()

                    if opts.Buffered then
                        p.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then stdoutSb.AppendLine(d.Data) |> ignore)
                        p.ErrorDataReceived .Add(fun d -> if not (isNull d.Data) then stderrSb.AppendLine(d.Data) |> ignore)
                    else
                        p.OutputDataReceived.Add(fun d -> if not (isNull d.Data) then (stdoutSb.AppendLine(d.Data) |> ignore; printfn "%s" d.Data))
                        p.ErrorDataReceived .Add(fun d -> if not (isNull d.Data) then (stderrSb.AppendLine(d.Data) |> ignore; eprintfn "%s" d.Data))

                    let started = p.Start()
                    if not started then
                        Error (sprintf "Failed to start runner: %s" runnerExe)
                    else
                        p.BeginOutputReadLine()
                        p.BeginErrorReadLine()

                        let timeoutMs = int opts.Timeout.TotalMilliseconds
                        let exited = p.WaitForExit(timeoutMs)
                        if not exited then
                            try p.Kill(true) with _ -> ()
                            Error "Timeout"
                        else
                            let outText =
                                if opts.Buffered then p.StandardOutput.ReadToEnd() else stdoutSb.ToString()
                            let errText =
                                if opts.Buffered then p.StandardError.ReadToEnd() else stderrSb.ToString()
                            Ok { ExitCode = p.ExitCode; StdOut = outText; StdErr = errText }

    let ensureWindows () : Result<unit,string> =
        if isWindows then Ok ()
        else Error "This command requires Windows (Visio COM). Run on Windows 10/11 with Microsoft Visio installed."


namespace VisioDiagramGenerator.Cli

open System
open System.Diagnostics
open System.IO
open System.Text

type RunResult = { ExitCode:int; StdOut:string; StdErr:string }

module Runner =
    let private isWindows =
        Environment.OSVersion.Platform = PlatformID.Win32NT

    let private onPath (exeName:string) : string option =
        Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator)
        |> Array.tryPick (fun p ->
            let candidate = Path.Combine(p, exeName)
            if File.Exists candidate then Some candidate else None)

    let private candidates () =
        let cwd = Directory.GetCurrentDirectory()
        [ Path.Combine(cwd, "src","VDG.VisioRuntime","bin","Debug","VDG.VisioRuntime.exe")
          Path.Combine(cwd, "src","VDG.VisioRuntime","bin","Release","VDG.VisioRuntime.exe")
          Path.Combine(cwd, "VDG.VisioRuntime.exe") ]

    let resolve (overridePath:string option) : Result<string,string> =
        match overridePath with
        | Some p when File.Exists p -> Ok p
        | Some p -> Error (sprintf "Runner not found: %s" (Path.GetFullPath p))
        | None ->
            let env = Environment.GetEnvironmentVariable("VDG_RUNNER")
            if not (String.IsNullOrWhiteSpace env) && File.Exists env then Ok env else
            match (candidates() |> List.tryFind File.Exists) with
            | Some p -> Ok p
            | None ->
                match onPath "VDG.VisioRuntime.exe" with
                | Some p -> Ok p
                | None -> Error "Runner not found. Provide --runner <path> or set VDG_RUNNER."

    type LaunchOptions =
        { ConfigPath : string
          RunnerPath : string
          Timeout    : TimeSpan
          Buffered   : bool
          PassThrough: string list }

    let run (opts: LaunchOptions) : Result<RunResult,string> =
        let args =
            let pt = String.Join(" ", opts.PassThrough)
            if String.IsNullOrWhiteSpace pt then
                sprintf "--config \"%s\"" opts.ConfigPath
            else
                sprintf "--config \"%s\" %s" opts.ConfigPath pt

        let psi = ProcessStartInfo(opts.RunnerPath, args)
        psi.UseShellExecute <- false
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.CreateNoWindow <- true

        use p = new Process()
        p.StartInfo <- psi

        let stdoutSb = StringBuilder()
        let stderrSb = StringBuilder()

        if not opts.Buffered then
            p.OutputDataReceived.Add(fun de -> if not (isNull de.Data) then stdoutSb.AppendLine(de.Data) |> ignore; Console.Out.WriteLine(de.Data))
            p.ErrorDataReceived.Add(fun de -> if not (isNull de.Data) then stderrSb.AppendLine(de.Data) |> ignore; Console.Error.WriteLine(de.Data))

        if not (p.Start()) then Error "Failed to start runner process." else
        if not opts.Buffered then (p.BeginOutputReadLine(); p.BeginErrorReadLine())

        let completed =
            if opts.Timeout = TimeSpan.Zero then p.WaitForExit() else p.WaitForExit(int opts.Timeout.TotalMilliseconds)

        if not completed then
            try p.Kill(true) with _ -> ()
            Error "Timeout"
        else
            let out =
                if opts.Buffered then p.StandardOutput.ReadToEnd() else stdoutSb.ToString()
            let err =
                if opts.Buffered then p.StandardError.ReadToEnd() else stderrSb.ToString()
            Ok { ExitCode = p.ExitCode; StdOut = out; StdErr = err }

    let ensureWindows () =
        if not isWindows then
            let msg = "This command requires Windows (Visio COM). Run on Windows 10/11 with Microsoft Visio installed."
            Error msg
        else Ok ()

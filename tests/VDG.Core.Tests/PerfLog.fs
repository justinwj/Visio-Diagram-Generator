namespace VDG.Core.Tests

open System
open System.Diagnostics
open System.IO

module PerfLog =
    let private artifacts = "artifacts"
    let private report = System.IO.Path.Combine(artifacts, "p7-test-report.txt")

    let private ensureArtifacts () =
        if not (Directory.Exists artifacts) then
            Directory.CreateDirectory artifacts |> ignore

    /// Time a function, append an ISO timestamp + label + ms to artifacts/p7-test-report.txt,
    /// and return the function result.
    let time (label: string) (f: unit -> 'T) : 'T =
        ensureArtifacts ()
        let sw = Stopwatch.StartNew()
        let result = f()
        sw.Stop()
        let line =
            String.Concat(
                DateTime.UtcNow.ToString("o"),
                "\t", label, "\t", string sw.ElapsedMilliseconds, "ms", Environment.NewLine)
        File.AppendAllText(report, line)
        result

module VisioDiagramGenerator.CliFs.Tests.ProgramTests

open System
open System.IO
open System.Reflection
open Xunit
open VDG.Core.Vba

let private programAssembly = typeof<VisioDiagramGenerator.CliFs.Command>.Assembly
let private programType = programAssembly.GetType("VisioDiagramGenerator.CliFs.Program")
let private mainMethod =
    match programType with
    | null -> None
    | t -> Option.ofObj (t.GetMethod("main", BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic))
let private setGatewayMethod =
    match programType with
    | null -> None
    | t -> Option.ofObj (t.GetMethod("setGatewayFactory", BindingFlags.Static ||| BindingFlags.NonPublic))

let private runProgram (args: string[]) : int =
    match mainMethod with
    | Some methodInfo -> methodInfo.Invoke(null, [| args :> obj |]) :?> int
    | None -> failwith "Program.main not found"

let private setGatewayFactory (factory: unit -> IVbeGateway) : unit =
    match setGatewayMethod with
    | Some methodInfo -> methodInfo.Invoke(null, [| factory :> obj |]) |> ignore
    | None -> failwith "Program.setGatewayFactory not found"

type private StubGateway() =
    interface IVbeGateway with
        member _.IsTrusted() = true
        member _.EnumerateModules() =
            seq { VbaModule("Module1", null) }
        member _.ExportModules(_projectPath: string) =
            seq { VbaModule("Module1", "Sub A()\n    Call B\nEnd Sub\nSub B()\nEnd Sub") }

[<Fact>]
let Generate_CreatesVsdxFile () =
    let tempModel = Path.GetTempFileName()
    let json = """{
  "schemaVersion": "1.1",
  "metadata": { "title": "Test Diagram" },
  "nodes": [
    {
      "id": "A",
      "label": "Node A",
      "groupId": "Group1",
      "size": { "width": 2.0, "height": 1.4 },
      "style": { "fill": "#CCE5FF", "stroke": "#004578" },
      "metadata": { "role": "start" }
    }
  ],
  "edges": []
}"""
    File.WriteAllText(tempModel, json)
    let outPath = Path.ChangeExtension(tempModel, ".vsdx")
    let originalSkip = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER", EnvironmentVariableTarget.Process)
    Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", "1", EnvironmentVariableTarget.Process)
    try
        let exitCode = runProgram [| "generate"; tempModel; "--output"; outPath |]
        Assert.Equal(0, exitCode)
        Assert.True(File.Exists(outPath))
    finally
        Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", originalSkip, EnvironmentVariableTarget.Process)
        if File.Exists(tempModel) then File.Delete(tempModel)
        if File.Exists(outPath) then File.Delete(outPath)
        Environment.SetEnvironmentVariable("VDG_SKIP_RUNNER", originalSkip, EnvironmentVariableTarget.Process)

[<Fact>]
let VbaAnalysis_WritesDiagramJson () =
    let tempProject = Path.ChangeExtension(Path.GetTempFileName(), ".xlsm")
    let outputPath = Path.ChangeExtension(tempProject, ".diagram.json")
    File.WriteAllText(tempProject, "placeholder")

    let resetGateway () = setGatewayFactory (fun () -> new ComVbeGateway() :> IVbeGateway)

    try
        setGatewayFactory (fun () -> new StubGateway() :> IVbeGateway)
        let exitCode = runProgram [| "vba-analysis"; tempProject; "--output"; outputPath |]
        Assert.Equal(0, exitCode)
        Assert.True(File.Exists(outputPath))
        let json = File.ReadAllText(outputPath)
        Assert.Contains("\"schemaVersion\"", json)
        Assert.Contains("\"nodes\"", json)
        Assert.Contains("Module1", json)
    finally
        resetGateway ()
        if File.Exists(tempProject) then File.Delete(tempProject)
        if File.Exists(outputPath) then File.Delete(outputPath)


namespace VisioDiagramGenerator.CliFs.Tests

open System.IO
open Xunit
open VisioDiagramGenerator.CliFs
open VDG.Core.Vba

type private StubGateway() =
    interface IVbeGateway with
        member _.IsTrusted() = true
        member _.EnumerateModules() =
            seq { VbaModule("Module1", null) }
        member _.ExportModules(_projectPath: string) =
            seq {
                VbaModule("Module1", "Sub A()\n    Call B\nEnd Sub\nSub B()\nEnd Sub")
            }

type ProgramTests() =
    [<Fact>]
    member _.Generate_CreatesVsdxFile() =
        // Create a temporary model JSON file
        let tempModel = Path.GetTempFileName()
        let json = "{""nodes"": [{""id"": ""A"", ""label"": ""A""}], ""edges"": []}"
        File.WriteAllText(tempModel, json)
        let outPath = Path.ChangeExtension(tempModel, ".vsdx")
        try
            let exitCode = Program.main [| "generate"; tempModel; "--output"; outPath |]
            Assert.Equal(0, exitCode)
            Assert.True(File.Exists(outPath))
        finally
            if File.Exists(tempModel) then File.Delete(tempModel)
            if File.Exists(outPath) then File.Delete(outPath)

    [<Fact>]
    member _.VbaAnalysis_WritesDiagramJson() =
        let tempProject = Path.ChangeExtension(Path.GetTempFileName(), ".xlsm")
        let outputPath = Path.ChangeExtension(tempProject, ".diagram.json")
        File.WriteAllText(tempProject, "placeholder")

        let resetGateway () = Program.setGatewayFactory(fun () -> new ComVbeGateway() :> IVbeGateway)

        try
            Program.setGatewayFactory(fun () -> new StubGateway() :> IVbeGateway)
            let exitCode = Program.main [| "vba-analysis"; tempProject; "--output"; outputPath |]
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


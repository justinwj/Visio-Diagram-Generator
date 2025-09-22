namespace VisioDiagramGenerator.CliFs.Tests

open System.IO
open Xunit
open VisioDiagramGenerator.CliFs

type N_ProgramTests() =
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
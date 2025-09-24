namespace VisioDiagramGenerator.CliFs.Tests

open Xunit
open VisioDiagramGenerator.CliFs

type CommandLineTests() =
    [<Fact>]
    member _.ParseGenerateWithOptions() =
        let args = [| "generate"; "model.json"; "--output"; "out.vsdx"; "--live-preview" |]
        match CommandLine.parse args with
        | Command.Generate(model, Some outPath, true) ->
            Assert.Equal("model.json", model)
            Assert.Equal("out.vsdx", outPath)
        | _ -> Assert.True(false, "Failed to parse generate command")

    [<Fact>]
    member _.ParseHelpForUnknownCommand() =
        let args = [| "unknown" |]
        match CommandLine.parse args with
        | Command.Help -> Assert.True(true)
        | _ -> Assert.True(false, "Unexpected command parsed")

namespace VDG.Core.Tests

open Xunit
open VDG.Core

module PipelineTests =

    [<Fact>]
    let ``buildCommands returns nodes then connectors`` () =
        let nA = { Id="A"; Label="Alpha"; Children=[]; Parent=None }
        let nB = { Id="B"; Label="Beta";  Children=[]; Parent=None }
        let c1 = { From="A"; To="B"; Label=None }

        let cmds = Pipeline.buildCommands [ nA; nB ] [ c1 ]
        Assert.Equal(3, cmds.Length)

        match cmds.[0], cmds.[1], cmds.[2] with
        | AddNode n1, AddNode n2, AddConnector c ->
            Assert.Equal("A", n1.Id)
            Assert.Equal("B", n2.Id)
            Assert.Equal("A", c.From)
            Assert.Equal("B", c.To)
        | _ -> failwith "Unexpected command ordering"

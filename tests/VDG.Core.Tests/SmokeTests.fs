namespace VDG.Core.Tests

open Xunit
open VDG.Core
open VDG.Core.Tests.PerfLog

module SmokeTests =

    [<Trait("Category", "Smoke")>]
    [<Fact>]
    let ``Prompt 7 smoke: pipeline → builder produces shapes/connectors`` () =
        let nodes =
            [ { Id="M"; Label="Main"; Children=[]; Parent=None }
              { Id="U"; Label="Util"; Children=[]; Parent=None }
              { Id="D"; Label="Data"; Children=[]; Parent=None } ]
        let conns =
            [ { From="M"; To="U"; Label=None }
              { From="M"; To="D"; Label=None } ]

        let cmds = time "smoke:pipeline-build" (fun () ->
            Pipeline.buildCommands nodes conns)

        Assert.True(cmds.Length > 0, "Expected at least one command")

        let svc = FakeVisioService()
        let layout = (GridLayout 12.0 :> ILayout)

        time "smoke:builder-execute" (fun () ->
            DiagramBuilder.execute (svc :> IVisioService) layout cmds)

        Assert.True(svc.ShapeCount >= 3, "Expected ≥ 3 shapes")
        Assert.True(svc.ConnectorCount >= 2, "Expected ≥ 2 connectors")

namespace VDG.Core.Tests

open Xunit
open VDG.Core
open VDG.Core.Tests.PerfLog

module DiagramBuilderTests =

    [<Fact>]
    let ``execute creates shapes then connectors (happy path)`` () =
        let nodes =
            [ { Id="A"; Label="Alpha"; Children=[]; Parent=None }
              { Id="B"; Label="Beta";  Children=[]; Parent=None } ]
        let conns =
            [ { From="A"; To="B"; Label=Some "A→B" } ]

        let cmds = Pipeline.buildCommands nodes conns

        let svc = FakeVisioService()
        let layout = (GridLayout 10.0 :> ILayout)

        time "unit:DiagramBuilder.execute" (fun () ->
            DiagramBuilder.execute (svc :> IVisioService) layout cmds)

        Assert.Equal(2, svc.ShapeCount)
        Assert.Equal(1, svc.ConnectorCount)

    [<Fact>]
    let ``execute ignores connectors that reference unknown nodes`` () =
        let nodes = [ { Id="X"; Label="X"; Children=[]; Parent=None } ]
        let conns =
            [ { From="X"; To="MISSING"; Label=None }
              { From="MISSING"; To="X"; Label=None }
              { From="X"; To="X"; Label=None } ]

        let cmds = Pipeline.buildCommands nodes conns

        let svc = FakeVisioService()
        let layout = (GridLayout 5.0 :> ILayout)

        DiagramBuilder.execute (svc :> IVisioService) layout cmds

        // Only the self-connection should be created.
        Assert.Equal(1, svc.ShapeCount)
        Assert.Equal(1, svc.ConnectorCount)

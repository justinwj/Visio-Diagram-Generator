PS C:\Users\justu\source\repos\Visio-Diagram-Generator> 
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> . 'C:\Users\justu\source\repos\Visio-Diagram-Generator\tools\render-smoke.ps1'
Running render smoke for 'tests/fixtures/vba/hello_world'...
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json "out\tmp\render_smoke.diagnostics.json" "out\tmp\render_smoke.diagram.json" "out\tmp\render_smoke.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\render_smoke.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Saved diagram: out\tmp\render_smoke.vsdx
OperationStopped: C:\Users\justu\source\repos\Visio-Diagram-Generator\tools\render-smoke.ps1:158:1                      
Line |
 158 |  $summaryObj = $serializer.DeserializeObject((Get-Content -Raw -Path $ …
     |  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | Could not load type 'System.Web.UI.WebResourceAttribute' from assembly 'System.Web, Version=4.0.0.0, Culture=neutral, 
     | PublicKeyToken=b03f5f7f11d50a3a'.
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> pwsh ./tools/render-smoke.ps1 -UpdateBaseline
Running render smoke for 'tests/fixtures/vba/hello_world'...
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json "out\tmp\render_smoke.diagnostics.json" "out\tmp\render_smoke.diagram.json" "out\tmp\render_smoke.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\render_smoke.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Saved diagram: out\tmp\render_smoke.vsdx
Baseline updated -> tests/baselines/render_diagnostics.json                                                             
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> pwsh ./tools/render-smoke.ps1 -UpdateBaseline
Running render smoke for 'tests/fixtures/vba/hello_world'...
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json "out\tmp\render_smoke.diagnostics.json" "out\tmp\render_smoke.diagram.json" "out\tmp\render_smoke.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\render_smoke.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Saved diagram: out\tmp\render_smoke.vsdx
Baseline updated -> tests/baselines/render_diagnostics.json                                                             
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> $env:VDG_SKIP_RUNNER = "0"
>> src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json out/tmp/hello.diagnostics.json out/tmp/hello.diagram.json out/tmp/hello.vsdx
input file not found: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello.diagram.json
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/hello_world --out out/tmp/hello.ir.json
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> $env:VDG_SKIP_RUNNER = "0"
>> src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json out/tmp/hello.diagnostics.json out/tmp/hello.diagram.json out/tmp/hello.vsdx
input file not found: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello.diagram.json
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/hello.ir.json --out out/tmp/hello.diagram.json --mode callgraph
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:3
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> $env:VDG_SKIP_RUNNER = "0"
>> src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json out/tmp/hello.diagnostics.json out/tmp/hello.diagram.json out/tmp/hello.vsdx
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello.diagnostics.json
Saved diagram: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello.vsdx
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/hello_world --out out/tmp/hello_full.vsdx --mode callgraph --diagram-json out/tmp/hello_full.diagram.json --diag-json out/tmp/hello_full.diagnostics.json
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe --diag-json "out/tmp/hello_full.diagnostics.json" "out/tmp/hello_full.diagram.json" "out/tmp/hello_full.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello_full.diagnostics.json
Saved diagram: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\hello_full.vsdx
Saved diagram: out/tmp/hello_full.vsdx
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> pwsh ./tools/render-smoke.ps1 -UpdateBaseline
Running render smoke for 'tests/fixtures/vba/hello_world'...
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json "out\tmp\render_smoke.diagnostics.json" "out\tmp\render_smoke.diagram.json" "out\tmp\render_smoke.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\render_smoke.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Saved diagram: out\tmp\render_smoke.vsdx
Baseline updated -> tests/baselines/render_diagnostics.json                                                             
PS C:\Users\justu\source\repos\Visio-Diagram-Generator> pwsh ./tools/render-smoke.ps1
Running render smoke for 'tests/fixtures/vba/hello_world'...
modules:1 procedures:1 edges:0 (progress)
modules:1 procedures:1 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:2
info: invoking C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe --diag-json "out\tmp\render_smoke.diagnostics.json" "out\tmp\render_smoke.diagram.json" "out\tmp\render_smoke.vsdx"
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:1
info: estimated straight-line crossings: 0
info: containers: 1
info: containers paddingIn=0.00in; cornerIn=0.12in
info: diagnostics JSON written: C:\Users\justu\source\repos\Visio-Diagram-Generator\out\tmp\render_smoke.diagnostics.json
info: skipping Visio runner due to VDG_SKIP_RUNNER
Saved diagram: out\tmp\render_smoke.vsdx
OperationStopped: C:\Users\justu\source\repos\Visio-Diagram-Generator\tools\render-smoke.ps1:158:1                      
Line |
 158 |  $summaryObj = $serializer.DeserializeObject((Get-Content -Raw -Path $ …
     |  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | Could not load type 'System.Web.UI.WebResourceAttribute' from assembly 'System.Web, Version=4.0.0.0, Culture=neutral, 
     | PublicKeyToken=b03f5f7f11d50a3a'.
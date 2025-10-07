
here is the ouput showing some warings,
```ps
PS D:\justinwj\Solutions\Visio Diagram Generator> dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/vba_callgraph.vsdx --mode callgraph
info: routing mode: orthogonal
info: connector count: 1
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:2
info: estimated straight-line crossings: 0
info: containers: 2
warning: sub-container 'Module1' overflows lane 'Modules'.
warning: sub-container 'Module2' overflows lane 'Modules'.
warning: sub-container 'Module1' overflows lane 'Modules'.
warning: sub-container 'Module2' overflows lane 'Modules'.
Saved diagram: D:\justinwj\Solutions\Visio Diagram Generator\out\tmp\vba_callgraph.vsdx
Saved diagram: out/tmp/vba_callgraph.vsdx
```

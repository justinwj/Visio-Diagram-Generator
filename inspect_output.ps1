PS D:\justinwj\Solutions\Visio Diagram Generator> pwsh ./tools/render-fixture.ps1 -FixtureName cross_module_calls ... 
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\cross_module_calls --out D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\callgraph\cross_module_calls.callgraph.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\callgraph\cross_module_calls.callgraph.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\callgraph\cross_module_calls.callgraph.diagram.json --mode callgraph
modules:2 procedures:2 edges:1 (progress)

Hyperlink Summary (callgraph):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Caller | Module1.bas | Module1 | 4 | 6 | Module1.bas#L4
Work | Module2.bas | Module2 | 4 | 6 | Module2.bas#L4
modules:2 procedures:2 edges:1 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:3
warning: view-mode diagram only has 2 node(s) for 2 module container(s); per-procedure nodes may be missing.
info: paging planner planned 1 page(s) (connector limit 400)
info: layer planner created 1 layer(s) (no cross-layer bridges).
info: routing mode: orthogonal
info: connector count: 1
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:2
info: bundles planned: 1 groups; max bundle size: 1
info: estimated straight-line crossings: 0
info: planned route crossings: 0; avg path length: 7.75in
info: containers: 2
info: containers paddingIn=0.20in; cornerIn=0.12in
warning: sub-container 'Module1' overflows lane 'Modules'.
warning: sub-container 'Module2' overflows lane 'Modules'.
warning: bundle separation 0.25in may be ineffective for 2 end(s) due to small node height; consider reducing layout.routing.bundleSeparationIn or increasing node heights.
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\callgraph\cross_module_calls.callgraph.diagnostics.json
info: layout outputMode=view
info: layout canvas 11.85in x 2.25in
info: layout nodes=2 modules=2 containers=2
info: planner summary modules=0 segments=0 delta=+0 splitModules=0 avgSegments/module=0.00 pages=1 avgModules/page=2.0 avgConnectors/page=2.0 maxOccupancy=16.7% maxConnectors=2 layers=1 overflowContainers=2
info: page 1 connectors=2 limit=400 modules=2
info: layer 1 modules=2 shapes=4 connectors=1
info: skipping Visio runner due to VDG_SKIP_RUNNER
Running: dotnet run --project src/VDG.VBA.CLI -- vba2json --in D:\justinwj\Solutions\Visio Diagram Generator\tests\fixtures\vba\cross_module_calls --out D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\module-structure\cross_module_calls.module-structure.ir.json --infer-metrics
Running: dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\module-structure\cross_module_calls.module-structure.ir.json --out D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\module-structure\cross_module_calls.module-structure.diagram.json --mode module-structure

Hyperlink Summary (module-structure):
Procedure/Control | File Path | Module | Start Line | End Line | Hyperlink
Caller | Module1.bas | Module1 | 4 | 6 | Module1.bas#L4
Work | Module2.bas | Module2 | 4 | 6 | Module2.bas#L4
modules:2 procedures:0 edges:0 dynamicSkipped:0 dynamicIncluded:0 progressEmits:0 progressLastMs:21
warning: view-mode diagram only has 2 node(s) for 2 module container(s); per-procedure nodes may be missing.
warning: view-mode diagram contains zero connectors; call edges may be missing.
info: paging planner planned 1 page(s) (connector limit 400)
info: layer planner created 1 layer(s) (no cross-layer bridges).
info: routing mode: orthogonal
info: connector count: 0
info: pagination analysis - usable height 7.50in, predicted pages 1.
info: lane 'Modules' nodes per page => 1:2
info: estimated straight-line crossings: 0
info: planned route crossings: 0; avg path length: 0.00in
info: containers: 2
info: containers paddingIn=0.20in; cornerIn=0.12in
warning: sub-container 'Module1' overflows lane 'Modules'.
warning: sub-container 'Module2' overflows lane 'Modules'.
info: diagnostics JSON written: D:\justinwj\Solutions\Visio Diagram Generator\...\cross_module_calls\module-structure\cross_module_calls.module-structure.diagnostics.json
info: layout outputMode=view
info: layout canvas 11.10in x 2.25in
info: layout nodes=2 modules=2 containers=2
info: planner summary modules=0 segments=0 delta=+0 splitModules=0 avgSegments/module=0.00 pages=1 avgModules/page=2.0 avgConnectors/page=0.0 maxOccupancy=16.7% maxConnectors=0 layers=1 overflowContainers=2
info: page 1 connectors=0 limit=400 modules=2
info: layer 1 modules=2 shapes=4 connectors=2
info: skipping Visio runner due to VDG_SKIP_RUNNER
Write-Error: D:\justinwj\Solutions\Visio Diagram Generator\tools\render-fixture.ps1:430:7
Line |
 430 |        Write-Error ("{0}/{1} {2}: {3}`nDiff hint: git diff --no-index  …
     |        ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | cross_module_calls/callgraph Diagram: Hash mismatch (expected
     | f7f8c70660ad1f0c9ae34bd82ae3ed8efc5e5662779e06a9574ee514807f4c71, actual
     | b35067343051b44402919538e11add12b028dbdd4f8159adcd2f3fa2fef37d78). Diff hint: git diff --no-index --
     | D:\justinwj\Solutions\Visio Diagram
     | Generator\tests\fixtures\render\cross_module_calls\callgraph\cross_module_calls.callgraph.diagram.json
     | D:\justinwj\Solutions\Visio Diagram
     | Generator\...\cross_module_calls\callgraph\cross_module_calls.callgraph.diagram.json
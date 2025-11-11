PS C:\Users\justu\source\repos\Visio-Diagram-Generator> dotnet test
  Determining projects to restore...
C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.CliFs\VisioDiagramGenerator.CliFs.fsproj : warning
 NU1902: Package 'Azure.Identity' 1.10.4 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-m5vv-6r
4h-3vj9 [C:\Users\justu\source\repos\Visio-Diagram-Generator\Visio-Diagram-Generator.sln]
C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.CliFs\VisioDiagramGenerator.CliFs.fsproj : warning
 NU1902: Package 'Azure.Identity' 1.10.4 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wvxc-85
5f-jvrv [C:\Users\justu\source\repos\Visio-Diagram-Generator\Visio-Diagram-Generator.sln]
  All projects are up-to-date for restore.
  VDG.Core.Contracts -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.Core.Contracts\bin\Debug\netstandard2.0\VDG.
  Core.Contracts.dll
  VDG.Core.Contracts -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.Core.Contracts\bin\Debug\net8.0\VDG.Core.Con
  tracts.dll
  VDG.VBA.CLI -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.VBA.CLI\bin\Debug\net8.0\VDG.VBA.CLI.dll
  VDG.Core -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.Core\bin\Debug\net8.0\VDG.Core.dll
  VDG.Core -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.Core\bin\Debug\netstandard2.0\VDG.Core.dll
C:\Program Files\dotnet\sdk\8.0.415\Microsoft.Common.CurrentVersion.targets(1890,5): warning NU1702: ProjectReference 'C:\Users\
justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\VDG.CLI.csproj' was resolved using '.NETFramework,Version=v4.8' instead o
f the project target framework '.NETCoreApp,Version=v8.0'. This project may not be fully compatible with your project. [C:\Users
\justu\source\repos\Visio-Diagram-Generator\tests\VDG.Core.Tests\VDG.Core.Tests.csproj]
  VDG.VBA.CLI.Tests -> C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.VBA.CLI.Tests\bin\Debug\net8.0\VDG.VBA.CLI.
  Tests.dll
Test run for C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.VBA.CLI.Tests\bin\Debug\net8.0\VDG.VBA.CLI.Tests.dll (.NETCoreApp,Version=v8.0)
C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.CliFs\VisioDiagramGenerator.CliFs.fsproj : warning
 NU1902: Package 'Azure.Identity' 1.10.4 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-m5vv-6r
4h-3vj9
C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.CliFs\VisioDiagramGenerator.CliFs.fsproj : warning
 NU1902: Package 'Azure.Identity' 1.10.4 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-wvxc-85
5f-jvrv
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
  VisioDiagramGenerator.Algorithms -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.Algorithms\b
  in\Debug\netstandard2.0\VisioDiagramGenerator.Algorithms.dll
  VDG.CLI -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe
  VisioDiagramGenerator.CliFs -> C:\Users\justu\source\repos\Visio-Diagram-Generator\src\VisioDiagramGenerator.CliFs\bin\Debug\n
  et8.0-windows\VisioDiagramGenerator.CliFs.dll
  VDG.Core.Tests -> C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.Core.Tests\bin\Debug\net8.0\VDG.Core.Tests.dll
Test run for C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.Core.Tests\bin\Debug\net8.0\VDG.Core.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
  VisioDiagramGenerator.CliFs.Tests -> C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VisioDiagramGenerator.CliFs.Tes
  ts\bin\Debug\net8.0-windows\VisioDiagramGenerator.CliFs.Tests.dll
Test run for C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VisioDiagramGenerator.CliFs.Tests\bin\Debug\net8.0-windows\VisioDiagramGenerator.CliFs.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 56 ms - VisioDiagramGenerator.CliFs.Tests.dll (net8.0)
[xUnit.net 00:00:00.85]     ContainersCliTests.Diagnostics_Reports_Containers_Count_And_Settings [FAIL]
[xUnit.net 00:00:00.86]     ContainersCliTests.Diagnostics_Warns_Unknown_Container_Assignments [FAIL]
  Failed ContainersCliTests.Diagnostics_Reports_Containers_Count_And_Settings [8 ms]
  Error Message:
   Assert.Contains() Failure
Not found: paddingIn=0.40in
In value:  info: paging planner planned 1 page(s) (connector limit 400)
           info: layer planner created 1 layer(s) (no cross-layer bridges).
           info: routing mode: orthogonal
           info: connector count: 0
           info: channels gapIn=1.20in; vertical corridors at X~ 1.27
           info: estimated straight-line crossings: 0
           info: planned route crossings: 0; avg path length: 0.00in
           info: containers: 2
           info: containers paddingIn=0.20in; cornerIn=0.12in
           warning: sub-container 'C_EXT' overflows lane 'External'.
           warning: sub-container 'C_SVC' overflows lane 'Services'.
           info: layout outputMode=view
           info: layout canvas 2.55in x 9.00in
           info: layout nodes=2 modules=2 containers=2
           info: planner summary modules=0 segments=0 delta=+0 splitModules=0 avgSegments/module=0.00 pages=1 avgModules/page=2.0 avgConnectors/page=0.0 maxOccupancy=8.3% maxConnectors=0 layers=1 overflowContainers=2
           info: page 1 connectors=0 limit=400 modules=2
           info: layer 1 modules=2 shapes=4 connectors=2
           info: skipping Visio runner due to VDG_SKIP_RUNNER
           warning: view-mode diagram only has 2 node(s) for 2 module container(s); per-procedure nodes may be missing.
           warning: view-mode diagram contains zero connectors; call edges may be missing.

  Stack Trace:
     at ContainersCliTests.Diagnostics_Reports_Containers_Count_And_Settings() in C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.Core.Tests\ContainersCliTests.cs:line 77
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
  Failed ContainersCliTests.Diagnostics_Warns_Unknown_Container_Assignments [4 ms]
  Error Message:
   Assert.Contains() Failure
Not found: unknown container
In value:  info: paging planner planned 1 page(s) (connector limit 400)
           info: layer planner created 1 layer(s) (no cross-layer bridges).
           info: routing mode: orthogonal
           info: connector count: 0
           info: estimated straight-line crossings: 0
           info: planned route crossings: 0; avg path length: 0.00in
           info: containers: 1
           info: containers paddingIn=0.20in; cornerIn=0.12in
           warning: sub-container 'MISSING' overflows lane 'External'.
           info: layout outputMode=view
           info: layout canvas 2.55in x 2.25in
           info: layout nodes=1 modules=1 containers=1
           info: planner summary modules=0 segments=0 delta=+0 splitModules=0 avgSegments/module=0.00 pages=1 avgModules/page=1.0 avgConnectors/page=0.0 maxOccupancy=8.3% maxConnectors=0 layers=1 overflowContainers=1
           info: page 1 connectors=0 limit=400 modules=1
           info: layer 1 modules=1 shapes=2 connectors=1
           info: skipping Visio runner due to VDG_SKIP_RUNNER
           warning: view-mode diagram only has 1 node(s) for 1 module container(s); per-procedure nodes may be missing.
           warning: view-mode diagram contains zero connectors; call edges may be missing.

  Stack Trace:
     at ContainersCliTests.Diagnostics_Warns_Unknown_Container_Assignments() in C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VDG.Core.Tests\ContainersCliTests.cs:line 107
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Failed!  - Failed:     2, Passed:     7, Skipped:     0, Total:     9, Duration: 96 ms - VDG.Core.Tests.dll (net8.0)
  VisioDiagramGenerator.Algorithms.Tests -> C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VisioDiagramGenerator.Algo
  rithms.Tests\bin\Debug\net8.0\VisioDiagramGenerator.Algorithms.Tests.dll
Test run for C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VisioDiagramGenerator.Algorithms.Tests\bin\Debug\net8.0\VisioDiagramGenerator.Algorithms.Tests.dll (.NETCoreApp,Version=v8.0)
VSTest version 17.11.1 (x64)

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.24]     VisioDiagramGenerator.Algorithms.Tests.ViewModePlannerTests.page contexts capture overflow splits [FAIL]
  Failed VisioDiagramGenerator.Algorithms.Tests.ViewModePlannerTests.page contexts capture overflow splits [< 1 ms]
  Error Message:
   Assert.NotEmpty() Failure: Collection was empty
  Stack Trace:
     at VisioDiagramGenerator.Algorithms.Tests.ViewModePlannerTests.page contexts capture overflow splits() in C:\Users\justu\source\repos\Visio-Diagram-Generator\tests\VisioDiagramGenerator.Algorithms.Tests\ViewModePlannerTests.fs:line 292
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)

Failed!  - Failed:     1, Passed:    16, Skipped:     0, Total:    17, Duration: 94 ms - VisioDiagramGenerator.Algorithms.Tests.dll (net8.0)

Passed!  - Failed:     0, Passed:    41, Skipped:     0, Total:    41, Duration: 1 m 26 s - VDG.VBA.CLI.Tests.dll (net8.0)
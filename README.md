![.NET CI](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml/badge.svg)


# VDG.Core.Contracts (compile-first)

Core contracts and compile-ready stubs for the **Visio Diagram Generator**:

- **Models**: basic diagram entities (`Node`, `Edge`, `DiagramModel`, `Point`, `Size`, `Bounds`, `ShapeStyle`, `ShapeDescriptor`).
- **Providers** *(interfaces)*: `IModelProvider`, `IDrawingSurface`, `IShapeCatalog`, `ISettingsProvider`.
- **Layouts** *(interfaces & DTOs)*: `ILayoutEngine`, `LayoutOptions`, `LayoutResult`, `NodeLayout`, `EdgeRoute`.
- **Pipeline**: `IPipelineStep`, `PipelineContext`, `DiagramPipeline`, placeholder steps (`ValidateModel`, `RunLayout`, `Render`).
- **DiagramBuilder**: high-level fa�ade to run the default pipeline.
- **Drawing**: command base & examples (`DrawCommand`, `DrawShape`, `DrawConnector`, `SetText`).
- **Logging**: minimal `ILogger`, `LogLevel`, `NullLogger`, `ConsoleLogger`.
- **ComSafety**: `ComSafety` helpers for safe COM disposal with `Marshal.FinalReleaseComObject`.

> No external dependencies, no COM interop references to Visio yet. Targets **.NET 8.0**.

## Build

```bash
dotnet build src/VDG.Core.Contracts/VDG.Core.Contracts.csproj -c Release
```

## Next

- Wire a concrete `IDrawingSurface` for Visio / SVG.
- Implement a working `ILayoutEngine` and replace placeholder steps as needed.
- Expand `ShapeStyle`, add text/format nuances, units, and measurement helpers.

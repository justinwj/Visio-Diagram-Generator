# Visio Diagram Generator

Generate Microsoft Visio diagrams from declarative JSON. The project ships with a Windows-only .NET Framework runner that drives Visio through COM automation, plus shared core libraries and layout helpers that can be reused by other front-ends.

![.NET CI](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml/badge.svg)

## What You Get
- **`VDG.CLI`** – Windows CLI (`net48`) that opens Visio via COM and renders diagrams described in JSON.
- **`VisioDiagramGenerator.CliFs`** – F# CLI (`net8.0-windows`) that can feed the .NET Framework runner.
- **Core libraries** – contracts, layout primitives, and algorithms shared across runners.
- **Samples & fixtures** – ready-to-run diagram JSON and schema hints to get started quickly.

## Prerequisites
- **Windows 10/11** with the **.NET SDK 8.0** (includes `dotnet` tooling).
- **.NET Framework 4.8 Developer Pack** (required to build `VDG.CLI`).
- **Microsoft Visio** (2019 or later recommended) installed and licensed on the machine running the CLI.
- **Microsoft Excel** (optional) if you rely on Excel-driven import workflows.
- A shell with access to `dotnet` (PowerShell or Windows Terminal works great).

> Tip: launch a Developer Command Prompt or ensure the .NET Framework targeting pack is installed if the `net48` build fails with targeting errors.

## Quick Start

1. **Clone and restore dependencies**
   ```powershell
   git clone https://github.com/justinwj/Visio-Diagram-Generator.git
   cd Visio-Diagram-Generator
   dotnet restore
   ```

2. **Build the entire solution**
   ```powershell
   dotnet build Visio-Diagram-Generator.sln -c Release
   ```
   The build produces `VDG.CLI.exe` under `src\VDG.CLI\bin\Release\net48\` and the F# runner under `src\VisioDiagramGenerator.CliFs\bin\Release\net8.0-windows\`.

3. **Prepare an input diagram JSON**
   Use the refreshed sample at `samples\sample_diagram.json` or craft your own based on the schema in `shared\Config\diagramConfig.schema.json`.

4. **Run the Windows CLI**
   ```powershell
   $cli = "src/VDG.CLI/bin/Release/net48/VDG.CLI.exe"
   $input = "samples/sample_diagram.json"
   $output = "out/sample-diagram.vsdx"
   & $cli $input $output
   ```
   When Visio automation succeeds you will see `Saved diagram: out/sample-diagram.vsdx` and the target `.vsdx` appears in the `out` folder. The CLI writes `<output>.error.log` if anything goes wrong.

5. **Open the result in Visio**
   Double-click the generated VSDX file to verify shapes, connectors, and styling.

## Diagram JSON Overview
A minimal envelope looks like this:
```json
{
  "schemaVersion": "1.1",
  "metadata": {
    "title": "Simple Flow",
    "description": "Optional details shown in Visio document properties."
  },
  "nodes": [
    {
      "id": "Start",
      "label": "Start",
      "style": { "fill": "#D6F5E5" }
    },
    {
      "id": "Finish",
      "label": "Finish"
    }
  ],
  "edges": [
    {
      "id": "Start->Finish",
      "sourceId": "Start",
      "targetId": "Finish",
      "label": "flow"
    }
  ]
}
```
- `nodes` translate to Visio shapes. Size can be specified via `size.width` / `size.height` (in inches). Styling supports hex fill/stroke colors and line patterns.
- `edges` become connectors. Set `directed: true` to add arrowheads.
- `metadata` is optional; unknown fields are ignored gracefully.

## Testing and Validation
- Run unit tests: `dotnet test --configuration Release`
- Rebuild after edits: `dotnet build -c Release`
- For CLI smoke tests, re-run the command in the quick start section using your scenario-specific JSON.

## Troubleshooting
- **Visio automation errors** (`RPC_E_DISCONNECTED`, `Visio automation error`, etc.): ensure Visio is installed, not already busy with modal dialogs, and that the CLI is executed from an STA-aware host (PowerShell works). The CLI automatically sets `[STAThread]` but recording macros or add-ins that lock the UI can still break automation.
- **Access denied when writing output**: confirm the target folder exists and you have write permissions. The CLI creates the directory tree when possible.
- **Build failures targeting `net48`**: install the .NET Framework 4.8 Developer Pack or use Visual Studio Build Tools with the desktop development workload.
- **Package vulnerability warnings**: `Azure.Identity` currently triggers NU1902 warnings. They are non-blocking for diagram generation but plan to update the dependency before shipping to production environments.

## Repository Layout
```
src/
  VDG.CLI/                     // Windows CLI runner (Visio automation)
  VisioDiagramGenerator.CliFs/ // F# CLI wrapper
  VDG.Core*/                   // Core contracts and implementations
samples/                       // Ready-to-run diagram JSON
shared/Config/                 // JSON schema and configuration samples
out/                           // Build + generated diagrams
```

## Contributing
Issues and pull requests are welcome. Please run `dotnet test` before submitting and include reproduction steps for Visio automation issues. The automation layer is sensitive to environment differences, so details about Visio version and Windows build help significantly.




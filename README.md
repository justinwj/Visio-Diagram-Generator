# Visio Diagram Generator

Generate Microsoft Visio diagrams from declarative JSON. The project ships with a Windows-only .NET Framework runner that drives Visio through COM automation, plus shared core libraries and layout helpers that can be reused by other front-ends.

![.NET CI](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml/badge.svg)

## What You Get
- `VDG.CLI` – Windows CLI (`net48`) that opens Visio via COM and renders diagrams described in JSON.
- `VisioDiagramGenerator.CliFs` – F# CLI (`net8.0-windows`) that can feed the .NET Framework runner.
- Core libraries – contracts, layout primitives, and algorithms shared across runners.
- Samples & fixtures – ready-to-run diagram JSON and schema hints to get started quickly.

## Prerequisites
- Windows 10/11 with the .NET SDK 8.0 (includes `dotnet` tooling).
- .NET Framework 4.8 Developer Pack (required to build `VDG.CLI`).
- Microsoft Visio (2019 or later recommended) installed and licensed on the machine running the CLI.
- Microsoft Excel (optional) if you rely on Excel-driven import workflows.
- A shell with access to `dotnet` (PowerShell or Windows Terminal works great).

> Tip: launch a Developer Command Prompt or ensure the .NET Framework targeting pack is installed if the `net48` build fails with targeting errors.

## Quick Start

1. Clone and restore dependencies
   ```powershell
   git clone https://github.com/justinwj/Visio-Diagram-Generator.git
   cd Visio-Diagram-Generator
   dotnet restore
   ```

2. Build the entire solution
   ```powershell
   dotnet build Visio-Diagram-Generator.sln -c Release
   ```
   The build produces `VDG.CLI.exe` under `src\VDG.CLI\bin\Release\net48\` and the F# runner under `src\VisioDiagramGenerator.CliFs\bin\Release\net8.0-windows\`.

3. Prepare an input diagram JSON
   Use a sample such as `samples\sample_architecture_layered.json` (schema 1.2) or `samples\sample_diagram.json` (simpler). See `shared\Config\diagramConfig.schema.json` for the full schema.

4. Run the Windows CLI
   - Direct invocation (recommended):
     ```powershell
     & "src\VDG.CLI\bin\Release\net48\VDG.CLI.exe" "samples\sample_architecture_layered.json" "out\sample-diagram.vsdx"
     ```
   - Or, using variables (avoid the PowerShell automatic variable `$input`):
     ```powershell
     $cli = "src\VDG.CLI\bin\Release\net48\VDG.CLI.exe"
     $inPath = "samples\sample_architecture_layered.json"
     $outPath = "out\sample-diagram.vsdx"
     & $cli $inPath $outPath
     ```
   Note: `$input` is a PowerShell automatic variable and will cause a parse error if used as shown above.
   When Visio automation succeeds you will see `Saved diagram: out/sample-diagram.vsdx` and the target `.vsdx` appears in the `out` folder. The CLI writes `<output>.error.log` if anything goes wrong.

   Optional diagnostics/spacing/page flags (override JSON):
   - `--diag-height <inches>` page-height threshold for overflow hints
   - `--diag-lane-max <int>` max nodes per lane before crowding hint
   - `--spacing-h <inches>` horizontal spacing between columns
   - `--spacing-v <inches>` vertical spacing between nodes
   - `--page-width <inches>` / `--page-height <inches>` / `--page-margin <inches>`
   - `--paginate <bool>` (reserved for future pagination)

5. Open the result in Visio
   Double-click the generated VSDX file to verify shapes, connectors, and styling.

## Diagram JSON Overview (Schema 1.2)
A minimal 1.2 envelope with lanes looks like this:
```json
{
  "schemaVersion": "1.2",
  "diagramType": "ServiceArchitecture",
  "layout": {
    "orientation": "horizontal",
    "tiers": ["External","Services","Data"],
    "spacing": { "horizontal": 1.2, "vertical": 0.6 },
    "page": { "widthIn": 11, "heightIn": 8.5, "marginIn": 0.75 }
  },
  "metadata": { "title": "Simple Flow", "description": "Optional details.", "version": "0.1", "author": "you" },
  "nodes": [
    { "id": "Start", "label": "Start", "tier": "External", "style": { "fill": "#D6F5E5" } },
    { "id": "Finish", "label": "Finish", "tier": "Services" }
  ],
  "edges": [
    { "id": "Start->Finish", "sourceId": "Start", "targetId": "Finish", "label": "flow" }
  ]
}
```
- `layout.orientation` and `layout.tiers` drive horizontal lanes; nodes can specify a `tier`. If omitted, nodes fall back to the first tier and a warning is emitted.
- `layout.diagnostics` (optional) accepts `pageHeightThresholdIn`, `laneMaxNodes`, and `enabled`; CLI flags override these per run.
- `nodes` translate to Visio shapes. Size via `size.width`/`size.height` (inches). Styling supports hex fill/stroke colors and line patterns.
- `edges` become connectors. Set `directed: true` to add arrowheads. Edge `metadata` accepts protocol/interface/mode for future labels.
- `metadata` adds optional `version`, `author`, and `createdUtc` in 1.2.

### What’s New in 1.2
- Tier-aware layered layout with lane containers (horizontal orientation implemented).
- Diagnostics with configurable thresholds (JSON + CLI overrides).
- Spacing and page configuration fields (used by layout and rendering).
- Additional document metadata and `diagramType` field.
- Backward compatible with 1.0/1.1 inputs.

Milestone 3 routing (alpha)
- `layout.routing` (mode, bundling, channels, routeAroundContainers) and `node.ports` (inSide/outSide) are now parsed by the CLI.
- Default routing mode is `orthogonal` using Visio Dynamic Connectors; set `--route-mode straight` to revert to straight lines.
- Additional flags: `--bundle-by`, `--bundle-sep`, `--channel-gap`, `--route-around` are accepted and stored for future bundling/channel algorithms.
- `edge.waypoints` and `edge.priority` are parsed and stored for future use.
- Testing note: set `VDG_SKIP_RUNNER=1` to skip the Visio automation in `VDG.CLI` during tests/CI. The CLI will still parse input, emit diagnostics, and create a stub output file.
 - Labels: polylines (corridors/waypoints) use detached label boxes placed near the longest segment. Tune with `edge.metadata["edge.label.offsetIn"]` (inches).
 - Diagnostics: now include baseline crossings, planned route crossings, average path length, channel utilization, edges-with-waypoints count, and a bundle-separation effectiveness warning for tiny shapes.

Milestone 4 containers (alpha)
- `layout.containers` adds container rendering options: `paddingIn` (inches), `cornerIn` (inches), and optional `style` (fill/stroke/linePattern).
- Optional top-level `containers[]` allows defining explicit sub-containers per tier. Nodes can opt-in via `containerId`.
- CLI flags: `--container-padding <in>` and `--container-corner <in>` override JSON; `--route-around <true|false>` prefers paths that skirt container edges.
- When `routeAroundContainers=true`, the CLI draws H–V–H polylines that hug lane container edges using roughly half the container padding, reducing overlap through container interiors.
- Diagnostics: logs container count, padding/corner values, and warns if nodes reference unknown container IDs.
- Sample: `samples/m4_containers_sample.json` (works with both Debug/Release builds).

Exported properties (Visio DocumentSheet)
- The CLI writes container metadata to the document’s ShapeSheet for downstream automation:
  - `User.ContainerCount` (numeric)
  - `User.ContainerIds` (CSV string)
  - `User.ContainerLabels` (CSV string)
  - `User.ContainerTiers` (CSV string)

Notes and Future Integration
- The layout model (nodes/edges + layout hints) is renderer-agnostic. While M1–M2 target Visio via COM, the same model can be exported to other formats (e.g., SVG, PPTX, PDF) in future milestones.

## Testing and Validation
- Run unit tests: `dotnet test --configuration Release`
- Rebuild after edits: `dotnet build -c Release`
- To test the CLI without Visio COM automation, set `VDG_SKIP_RUNNER=1`.
- For CLI smoke tests, re-run the command in the quick start section using your scenario-specific JSON.

## Samples
- Corridor-aware routing sample: `samples/m3_dense_sample.json`
- Cross-lane stagger stress sample: `samples/m3_crosslane_stress.json`
 - Tiny-shapes bundle-separation warning: `samples/m3_tiny_bundle_warning.json`
- Containers (M4) sample: `samples/m4_containers_sample.json`
- Generate (skip Visio):
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_dense_sample.json out/m3_dense_sample.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_dense_sample.json out\m3_dense_sample.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_crosslane_stress.json out/m3_crosslane_stress.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_crosslane_stress.json out\m3_crosslane_stress.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_tiny_bundle_warning.json out/m3_tiny_bundle_warning.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_tiny_bundle_warning.json out\m3_tiny_bundle_warning.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m4_containers_sample.json out/m4_containers_sample.vsdx`
  
  Direct run of built CLI (Visio required):
  - PowerShell: `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" "samples\m4_containers_sample.json" "out\m4_containers_sample.vsdx"`

## Troubleshooting
- Visio automation errors (`RPC_E_DISCONNECTED`, `Visio automation error`, etc.): ensure Visio is installed, not already busy with modal dialogs, and that the CLI is executed from an STA-aware host (PowerShell works). The CLI automatically sets `[STAThread]` but recording macros or add-ins that lock the UI can still break automation.
- Access denied when writing output: confirm the target folder exists and you have write permissions. The CLI creates the directory tree when possible.
- Build failures targeting `net48`: install the .NET Framework 4.8 Developer Pack or use Visual Studio Build Tools with the desktop development workload.
- Package vulnerability warnings: `Azure.Identity` currently triggers NU1902 warnings. They are non-blocking for diagram generation but will be updated before production use.

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

## Roadmap
- Milestone 2 (current): tiered layout, spacing, pagination/banding, title banner, diagnostics. See `docs/VDG_MILESTONE_TWO.md`.
- Milestone 3 (next): improved connector routing (orthogonal), bundling, and reserved channels to reduce overlap. Plan in `docs/VDG_MILESTONE_THREE.md`.

## Contributing
Issues and pull requests are welcome. Please run `dotnet test` before submitting and include reproduction steps for Visio automation issues. The automation layer is sensitive to environment differences, so details about Visio version and Windows build help significantly.

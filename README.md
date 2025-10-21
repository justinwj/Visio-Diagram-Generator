# Visio Diagram Generator

Generate Microsoft Visio diagrams from declarative JSON. The project ships with a Windows-only .NET Framework runner that drives Visio through COM automation, plus shared core libraries and layout helpers that can be reused by other front-ends.

![.NET CI](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml/badge.svg) [![Perf Smoke](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml/badge.svg)](https://github.com/justinwj/Visio-Diagram-Generator/actions/workflows/dotnet.yml)

CI Notes
- The main workflow runs unit tests across projects and validates Diagram JSON against schema 1.2.
- A validation matrix job runs `ir2diagram` in both default and `--strict-validate` modes to prevent regressions.
- A perf-smoke job emits timing and counts for IR→Diagram conversions and uploads metrics (`out/perf/perf.json`).
 - The perf-smoke job writes a Job Summary with key metrics (vba2json/ir2diagram ms, nodes, edges, dynamicSkipped/dynamicIncluded). Open any workflow run and click the “perf-smoke” job to view the summary and download artifacts.

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
   - Diagnostics (legacy hints):
     - `--diag-height <inches>` page-height threshold for overflow hints
     - `--diag-lane-max <int>` max nodes per lane before crowding hint
   - Diagnostics (M5):
     - `--diag-level <info|warning|error>` minimum severity to emit
     - `--diag-lane-warn <0..1>` lane occupancy warn ratio (default 0.85)
     - `--diag-lane-error <0..1>` lane occupancy error ratio (default 0.95)
     - `--diag-page-warn <0..1>` page/band occupancy warn ratio (default 0.90)
     - `--diag-cross-warn <n>` planned crossings warn threshold (default 200)
     - `--diag-cross-err <n>` planned crossings error threshold (default 400)
   - `--diag-util-warn <0..100>` corridor utilization warn minimum percent (default 40)
   - `--diag-json [path]` write structured diagnostics JSON (default `<output>.diagnostics.json`)
  - `--spacing-h <inches>` horizontal spacing between columns
  - `--spacing-v <inches>` vertical spacing between nodes
  - `--page-width <inches>` / `--page-height <inches>` / `--page-margin <inches>`
  - `--paginate <bool>` (reserved for future pagination)

   After layout, the CLI prints a planner summary that surfaces pagination health at a glance. Example:
   ```
   info: planner summary modules=210 segments=248 delta=+38 splitModules=37 avgSegments/module=1.18 pages=238 avgModules/page=1.0 avgConnectors/page=9.2 maxOccupancy=250.0% maxConnectors=48
   ```
   The summary shows how many original modules were split into height-bounded segments, the resulting page count, and the peak occupancy/connectors per page so you can spot overflow hot spots without opening diagnostics JSON.

   Diagnostics JSON contents (when enabled):
   - `metrics.connectorCount`, `metrics.straightLineCrossings`, `metrics.pageHeight`, `metrics.usableHeight`
   - `metrics.moduleCount`, `metrics.segmentCount`, `metrics.segmentDelta`, `metrics.splitModuleCount`, `metrics.averageSegmentsPerModule`
   - `metrics.plannerPageCount`, `metrics.plannerAverageModulesPerPage`, `metrics.plannerAverageConnectorsPerPage`, `metrics.plannerMaxOccupancyPercent`, `metrics.plannerMaxConnectorsPerPage`
   - `metrics.lanePages[] { tier, page, occupancyRatio, nodes }`
   - `metrics.containers[] { id, tier, page, occupancyRatio, nodes }` (when containers present and page height configured)
   - `issues[] { code, level, message, lane?, page? }` (respects `--diag-level`; includes `LaneCrowding`, `PageCrowding`, `PageOverflow`, `ContainerOverflow`, `ContainerCrowding`)

   Example (containers excerpt):
   ```json
   {
     "metrics": {
       "containers": [
         { "id": "Z_SVC", "tier": "Services", "page": 1, "occupancyRatio": 0.42, "nodes": 3 }
       ]
     },
     "issues": [
       { "code": "ContainerOverflow", "level": "warning", "lane": "Services", "page": 1, "message": "sub-container 'Z_SVC' overflows lane 'Services'." }
     ]
   }
   ```

   Interpreting `metrics.containers[]`
   - Occupancy formula: `sum(nodeHeightsOnPage) + (nodesOnPage - 1) * verticalSpacing` divided by `usableHeight`.
   - `usableHeight` = `pageHeight - (2 * pageMargin) - titleHeight` (titleHeight is 0.6in when a title is present, else 0.0).
   - Thresholds: uses lane thresholds for severity — `laneCrowdWarnRatio` (default 0.85) and `laneCrowdErrorRatio` (default 0.95).
   - Gating: console and JSON issues respect `--diag-level` / `layout.diagnostics.level`.

   Tips
   - CrossingDensity and LowUtilization use the planned routing estimate; set `layout.routing.channels.gapIn` (or `--channel-gap`) to enable corridor planning.
   - To reproduce a low utilization warning quickly, use `samples\m5_low_utilization.json` with `--diag-util-warn 60`.

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

## Testing and Validation
- Run unit tests: `dotnet test --configuration Release`
- Rebuild after edits: `dotnet build -c Release`
- To test the CLI without Visio COM automation, set `VDG_SKIP_RUNNER=1`.
- For CLI smoke tests, re-run the command in the quick start section using your scenario-specific JSON.

## Paging Planner Reference
The pagination summary printed by the CLI and the associated diagnostics metrics are documented in `docs/PagingPlanner.md`. Review that guide when tuning thresholds, interpreting fixture output (`render-fixture.ps1`), or onboarding new team members to the segmentation heuristics.

## Samples
- Corridor-aware routing sample: `samples/m3_dense_sample.json`
- Cross-lane stagger stress sample: `samples/m3_crosslane_stress.json`
 - Tiny-shapes bundle-separation warning: `samples/m3_tiny_bundle_warning.json`
- Containers (M4) sample: `samples/m4_containers_sample.json`
 - Dense tier (M5) sample (triggers lane crowding): `samples/m5_dense_tier.json`
 - Crossing density (M5) sample: `samples/m5_crossing_density.json`
- Generate (skip Visio):
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_dense_sample.json out/m3_dense_sample.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_dense_sample.json out\m3_dense_sample.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_crosslane_stress.json out/m3_crosslane_stress.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_crosslane_stress.json out\m3_crosslane_stress.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m3_tiny_bundle_warning.json out/m3_tiny_bundle_warning.vsdx`
  - cmd.exe: `set VDG_SKIP_RUNNER=1 && dotnet run --project src\VDG.CLI -- samples\m3_tiny_bundle_warning.json out\m3_tiny_bundle_warning.vsdx`
  - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- samples/m4_containers_sample.json out/m4_containers_sample.vsdx`
    - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- --diag-json --diag-lane-warn 0.80 --diag-lane-error 0.90 --diag-page-warn 0.90 samples/m5_dense_tier.json out/m5_dense_tier.vsdx`
    - PowerShell: `$env:VDG_SKIP_RUNNER=1; dotnet run --project src/VDG.CLI -- --diag-json out/m5_crossing_density.diag.json --diag-cross-warn 1 samples/m5_crossing_density.json out/m5_crossing_density.vsdx`
  
  Direct run of built CLI (Visio required):
    - PowerShell: `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" "samples\m4_containers_sample.json" "out\m4_containers_sample.vsdx"`
    - PowerShell: `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" --diag-json --diag-lane-warn 0.80 --diag-lane-error 0.90 --diag-page-warn 0.90 "samples\m5_dense_tier.json" "out\m5_dense_tier.vsdx"`
    - PowerShell: `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" --diag-json --diag-cross-warn 1 "samples\m5_crossing_density.json" "out\m5_crossing_density.vsdx"`

## Troubleshooting
- Visio automation errors (`RPC_E_DISCONNECTED`, `Visio automation error`, etc.): ensure Visio is installed, not already busy with modal dialogs, and that the CLI is executed from an STA-aware host (PowerShell works). The CLI automatically sets `[STAThread]` but recording macros or add-ins that lock the UI can still break automation.
- Access denied when writing output: confirm the target folder exists and you have write permissions. The CLI creates the directory tree when possible.
- `--diag-json` without a path: the next non-flag token is treated as the JSON path. If you omit a path, the CLI writes `<output>.diagnostics.json` next to your `.vsdx`. To avoid consuming your input path accidentally, either provide an explicit JSON path or place another flag immediately after `--diag-json` before the input and output paths.
- Build failures targeting `net48`: install the .NET Framework 4.8 Developer Pack or use Visual Studio Build Tools with the desktop development workload.
- Package vulnerability warnings: `Azure.Identity` currently triggers NU1902 warnings. They are non-blocking for diagram generation but will be updated before production use.
- CrossingDensity uses the planned-routing estimate; enable corridors via `layout.routing.channels.gapIn` (or `--channel-gap`) and prefer multiple nodes per lane for a more representative crossing count.

## VBA CLI
- See `docs/VDG_VBA_CLI.md` for a reusable CLI that converts VBA sources to IR JSON (`vba2json`) and IR JSON to Diagram JSON (`ir2diagram`).
- End-to-end example (PowerShell):
  - `dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/ir.json`
  - `./tools/ir-validate.ps1 -InputPath out/tmp/ir.json`
  - `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir.json --out out/tmp/diagram.json`
  - `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" out/tmp/diagram.json out/tmp/diagram.vsdx`
 - Modes:
   - `--mode callgraph` (default): per-procedure nodes + call edges
   - `--mode module-structure`: procedures grouped in module containers; no edges
   - `--mode module-callmap`: module-level call aggregation edges (N call(s))
   - `--mode event-wiring`: control events → handler procedures for Form modules
 - One-shot render convenience:
   - `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out out/diagram.vsdx --mode callgraph`
   - The `render` command auto-discovers `VDG.CLI.exe` (or use `--cli` or `VDG_CLI` env).

## Repository Layout
```
src/
  DebugHarness/                      // Scratch runner for local experimentation
  VDG.CLI/                           // Windows CLI that drives Visio via COM
  VDG.VBA.CLI/                       // VBA export pipeline (vba2json / ir2diagram / render)
  VDG.Core/                          // Core implementation shared across runners
  VDG.Core.Contracts/                // Shared contracts/DTOs consumed by clients
  VDG.VisioRuntime/                  // Visio automation helpers (shapes, masters, etc.)
  VisioDiagramGenerator.Algorithms/  // Layout algorithms and routing helpers
  VisioDiagramGenerator.CliFs/       // F# command-line wrapper
docs/                                // Specs, governance, design notes
samples/                             // Ready-to-run diagram JSON and VBA exports
shared/Config/                       // JSON schema and default configuration snippets
tests/                               // Unit, integration, and CLI smoke tests
tools/                               // PowerShell helpers (fixtures, validation, perf smoke)
out/                                 // Build + generated artifacts (gitignored)
```

## Upcoming Work
- Continue refining routing and bundling behaviour (channels, reserved corridors, waypoint handling).
- Expand reusable CLI tooling and summaries so downstream automation can validate hyperlinks and metrics without Visio.
- Explore additional runners that do not require COM automation to broaden platform support.

## Contributing
Issues and pull requests are welcome. Please run `dotnet test` before submitting and include reproduction steps for Visio automation issues. The automation layer is sensitive to environment differences, so details about Visio version and Windows build help significantly.

### IR / Schema Onboarding
1. Read the VBA IR specification in `docs/VBA_IR.md` to understand entities, schema shape, and examples.
2. Follow the governance checklist and smoke workflow in `docs/IR_Governance.md` before proposing IR changes.
3. Review the terminology in `docs/Glossary.md` so you recognise project-specific acronyms during reviews.

> Automated guardrails: the **PR Checklist Enforcement** workflow blocks merges unless all IR Impact items are checked or a justified exception rationale is provided.

# VDG VBA CLI

Purpose
- Reusable CLI for converting VBA sources → IR JSON (vba2json) and IR JSON → Diagram JSON (ir2diagram), suitable for automation and piping into `VDG.CLI`.

Commands
- vba2json: Parse exported VBA files and emit IR JSON v0.2
  - Usage:
    - `dotnet run --project src/VDG.VBA.CLI -- vba2json --in <folder> [--glob <pattern> ...] [--out <ir.json>] [--project-name <name>] [--root <path>] [--infer-metrics]`
  - Inputs: folder containing `.bas/.cls/.frm`; optional glob patterns limit which files are included
  - Outputs: IR JSON to stdout if `--out` omitted; writes to file when provided
  - Exit codes: 0 success; 65 invalid input; 70 internal error

- ir2diagram: Convert IR JSON into diagram JSON (Project Call Graph)
  - Usage:
    - `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>]`
  - Inputs: IR JSON conforming to `shared/Config/vbaIr.schema.json`
  - Outputs: Diagram JSON to stdout if `--out` omitted; writes to file when provided
  - Exit codes: 0 success; 65 invalid input; 70 internal error

- render: Pipeline helper that runs `vba2json`, `ir2diagram`, and `VDG.CLI`
  - Usage:
    - `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out <diagram.vsdx> [--mode <callgraph|module-structure|module-callmap>] [--cli <VDG.CLI.exe>] [--diagram-json <path>] [--diag-json <path>]`
  - Outputs: `.vsdx` (and optional diagram JSON / diagnostics JSON when the extra paths are provided)
  - Exit codes: 0 success; 65 invalid input; 70 internal error
  - When `--diag-json <path>` is provided, the command forwards the request to `VDG.CLI` so that structured diagnostics land alongside the diagram.
  - Environment overrides honoured during render:
    - `VDG_DIAG_LANE_WARN`, `VDG_DIAG_LANE_ERR`, `VDG_DIAG_PAGE_WARN` – tweak lane/page crowding ratios (0..1).
    - `VDG_DIAG_FAIL_LEVEL` (or legacy `VDG_DIAG_FAIL_ON`) – fail the render when diagnostics reach `warning` or `error`.
    - `VDG_SKIP_RUNNER=1` – skip the Visio COM automation (used in CI smoke runs).

Flags & Behavior
- `--in` and `--out` are positional as shown. When `--out` is omitted, the tool prints JSON to stdout (for streaming workflows).
- `--project-name` overrides the project name detected from input folder (vba2json only).
- `--glob <pattern>` (repeatable) filters the discovered files using `*`/`?` wildcards relative to `--in`. All matches merge into a single IR project.
- `--root <path>` controls how module files are relativised inside the IR; defaults to the `--in` directory.
- `--infer-metrics` toggles lightweight `metrics.lines` output on modules and procedures. Metrics are omitted by default to keep payloads lean.
- The vba2json parser is a pragmatic skeleton: it recognizes procedure signatures and simple `Module.Proc` call patterns, and tags dynamic calls (CallByName/Application.Run).

Examples
```powershell
# 1) Sources -> IR -> Validate
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/ir_cross.json
./tools/ir-validate.ps1 -InputPath out/tmp/ir_cross.json

# 2) IR -> Diagram JSON -> Render
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json
& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" out/tmp/ir_cross.diagram.json out/tmp/ir_cross.vsdx

# 3) Single command render + diagnostics
dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/render.vsdx --mode callgraph --diagram-json out/tmp/render.diagram.json --diag-json out/tmp/render.diagnostics.json

# Optional: fail the build when diagnostics escalate to error
$env:VDG_DIAG_FAIL_LEVEL = "error"
dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/render_strict.vsdx --diag-json out/tmp/render_strict.diagnostics.json
```

Notes
- The CLI writes helpful usage messages on invalid arguments. Use `--help` to see supported commands.
- Module-name collisions or missing glob matches result in descriptive errors on stderr and exit code 65.
- For robust parsing beyond the skeleton, enhancements will be tracked in a subsequent milestone (types/params/returns, more call patterns, VBIDE integration).
- Alias handling: `vba2json` now tracks simple `Set alias = ...` assignments, pulling return-type metadata (`Module.Function As Type`) so `worker.Factory().RunAll` resolves to `Helper.RunAll`; targets fall back to the qualifier type when return types are unknown.
- Inside `With` blocks, chained member calls (e.g., `.Factory().RunAll`) reuse the same inference pipeline, yielding both the intermediate call (`Worker.Factory`) and the resolved helper (`Helper.RunAll`).
- Limitations: alias inference trims trailing inline comments but still falls back when member chains lose type info, and dynamic invocation (`CallByName`, `Application.Run`) still emits `~unknown` targets.
- Return type lookups rely on explicit `As Type` in the signature; late-bound factories or Property Let/Set cannot yet feed alias inference.
- CFG mode (`--mode proc-cfg`) emits decision (`#dec`) and loop (`#loop`) scaffolds, and now surfaces combined loop-with-branch patterns with explicit `Else` nodes/back edges (see `tests/fixtures/vba/cfg_nested`); deeper nesting remains on the roadmap.

## Updates (Milestone 8)

- ir2diagram usage now supports modes and unknown-edge inclusion:
  - `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>] [--mode <callgraph|module-structure|module-callmap|event-wiring|proc-cfg>] [--include-unknown] [--timeout <ms>]`
- When `--out` is used, ir2diagram prints a summary line to stdout: `modules:N procedures:M edges:E dynamicSkipped:D dynamicIncluded:X progressEmits:Y progressLastMs:Z`.
- Invalid IR (malformed JSON) is reported as a usage error with exit code 65.
- `--timeout <ms>` aborts long IR-to-Diagram conversions gracefully with a clear error.
- Callgraph diagnostics can be tuned via environment variables: `VDG_CALLGRAPH_FANOUT_THRESHOLD`, `VDG_CALLGRAPH_SELF_CALL_SEVERITY`, and `VDG_CALLGRAPH_FANOUT_SEVERITY`.

### Validation Options
- `--strict-validate`: Enable stricter IR validation.
  - Enforces module/procedure presence, valid `module.kind`, well‑formed `locs`, and consistent dynamic call metadata (e.g., `target == "~unknown"` must include `isDynamic = true`).
  - Use in production/enterprise pipelines where IR quality is critical.
  - Example:
    ```powershell
    dotnet run --project src/VDG.VBA.CLI -- ir2diagram --strict-validate --in out/tmp/project.ir.json --out out/tmp/project.diagram.json --mode callgraph
    ```

### Troubleshooting
- When to use strict vs default:
  - Default mode is flexible and tolerant (skips unknown dynamic targets and missing optional fields). Use this for iterative development or exploratory parsing.
  - `--strict-validate` is for production pipelines and CI gates where IR quality must be guaranteed.
- Common validation failures (strict mode):
  - "IR contains no modules/procedures" → Ensure your input folder/globs produced content; verify you’re pointing `--in` at the exported `.bas/.cls/.frm` root.
  - "Module has invalid kind/missing file" → Confirm `Attribute VB_Name` was parsed and `.bas/.cls/.frm` are mapped to `Module|Class|Form` with known relative file paths.
  - "Procedure has invalid locs" → Line positions must be present and sane (1-based, endLine ≥ startLine); re-export sources if line mapping drifted.
  - "Call has '~unknown' target but isDynamic=false" → For `CallByName`/`Application.Run` ensure IR sets `isDynamic=true`; this is automatic in the current generator.
  - "Call missing site information" → Each call must include `site.module|file|line`; re-run vba2json on original exports.
- Performance implications:
  - Strict validation adds linear checks over modules/procedures/calls; impact is small for most projects (<5–10% in local smoke). Prefer enabling only in CI/production if you are very latency‑sensitive during local iterations.

### Examples (Milestone 8 additions)

```powershell
# Callgraph mode with explicit --mode
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json --mode callgraph

# Include unknown dynamic calls for debugging
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/dynamic_calls --out out/tmp/ir_dynamic.json
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_dynamic.json --out out/tmp/ir_dynamic.diagram.json --mode callgraph --include-unknown
# stdout summary (when --out is used):
# modules:N procedures:M edges:E dynamicSkipped:D dynamicIncluded:X progressEmits:Y progressLastMs:Z

# Bad scenario: invalid IR path or malformed JSON
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in tests/fixtures/ir/invalid.json
# stderr: usage: Invalid IR JSON.
# exit code: 65
```

### Sample Callgraph Diagram

- Path: `samples/vba_callgraph.diagram.json` (generated from `tests/fixtures/vba/cross_module_calls` via `ir2diagram --mode callgraph`).
- Schema: validated against `shared/Config/diagramConfig.schema.json` (1.2).
- Highlights: node metadata includes `code.module`, `code.proc`, `code.kind`, `code.access`, and `code.locs.*`; call edges carry `code.edge` and `code.site.*`.
- Generation summary metrics: `modules:2 procedures:2 edges:1 dynamicSkipped:0 dynamicIncluded:0 progressEmits:1 progressLastMs:<ms>` (values recorded when producing the sample).
- Tests ensure the sample stays in sync with CLI output and surface metadata expectations (`ParserSmokeTests.SampleCallgraphDiagram_MatchesFixture`).

### Performance Smoke

- Script: `tools/perf-smoke.ps1`
- Purpose: quick timing/memory snapshot for IR→Diagram conversion.
- Usage:
  ```powershell
  pwsh ./tools/perf-smoke.ps1 -In tests/fixtures/vba/cross_module_calls -Mode callgraph -TimeoutMs 15000
  ```
- Output: writes IR/Diagram to `out/tmp`, prints elapsed ms for `vba2json` and `ir2diagram`, shows shell working set, and emits structured JSON metrics to `out/perf/perf.json` (included as a CI artifact; see `.github/workflows/dotnet.yml`, job `perf-smoke`). The CI job also publishes a Job Summary with key metrics for quick inspection.
- Perf artifact summary now includes progress metadata (`progress.emits`, `progress.lastMs`) sourced from ir2diagram progress reporting.

### Render Diagnostics Thresholds (Milestone 9)

- Defaults: lane crowding warning at >= 0.85 occupancy, lane overcrowding error at >= 0.95, page crowding warning at >= 0.90.
- Environment overrides: set `VDG_DIAG_LANE_WARN`, `VDG_DIAG_LANE_ERR`, or `VDG_DIAG_PAGE_WARN` (ratios 0..1) before running `VDG.CLI` to adjust thresholds without editing Diagram JSON.
- Fail-level control: leave unset for warn-only CI, or set `VDG_DIAG_FAIL_LEVEL=warning|error` to exit with code 65 when diagnostics reach that severity.
- Example:
  ```powershell
  $env:VDG_DIAG_LANE_WARN = "0.80"
  $env:VDG_DIAG_FAIL_LEVEL = "error"
  dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/render.vsdx
  ```
- CI guidance: defaults keep pipelines informational; opt into failure only once thresholds are tuned.
- Smoke regression: `tools/render-smoke.ps1` exercises the entire pipeline, writes `out/perf/render_diagnostics.json`, and compares against `tests/baselines/render_diagnostics.json` with a ±5% tolerance for identical fixtures. Use `-UpdateBaseline` after intentional styling/layout changes.


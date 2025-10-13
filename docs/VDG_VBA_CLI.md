# VDG VBA CLI

Purpose
- Reusable CLI for converting VBA sources → IR JSON (vba2json) and IR JSON → Diagram JSON (ir2diagram), suitable for automation and piping into `VDG.CLI`.

Commands
- vba2json: Parse exported VBA files and emit IR JSON v0.1
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

Flags & Behavior
- `--in` and `--out` are positional as shown. When `--out` is omitted, the tool prints JSON to stdout (for streaming workflows).
- `--project-name` overrides the project name detected from input folder (vba2json only).
- `--glob <pattern>` (repeatable) filters the discovered files using `*`/`?` wildcards relative to `--in`. All matches merge into a single IR project.
- `--root <path>` controls how module files are relativised inside the IR; defaults to the `--in` directory.
- `--infer-metrics` toggles lightweight `metrics.lines` output on modules and procedures. Metrics are omitted by default to keep payloads lean.
- The vba2json parser is a pragmatic skeleton: it recognizes procedure signatures and simple `Module.Proc` call patterns, and tags dynamic calls (CallByName/Application.Run).

Examples
```powershell
# 1) Sources → IR → Validate
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/ir_cross.json
./tools/ir-validate.ps1 -InputPath out/tmp/ir_cross.json

# 2) IR → Diagram JSON → Render
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json
& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" out/tmp/ir_cross.diagram.json out/tmp/ir_cross.vsdx
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
- When `--out` is used, ir2diagram prints a summary line to stdout: `modules:N procedures:M edges:E dynamicSkipped:D dynamicIncluded:X`.
- Invalid IR (malformed JSON) is reported as a usage error with exit code 65.
- `--timeout <ms>` aborts long IR→Diagram conversions gracefully with a clear error.

### Examples (Milestone 8 additions)

```powershell
# Callgraph mode with explicit --mode
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json --mode callgraph

# Include unknown dynamic calls for debugging
dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/dynamic_calls --out out/tmp/ir_dynamic.json
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_dynamic.json --out out/tmp/ir_dynamic.diagram.json --mode callgraph --include-unknown
# stdout summary (when --out is used):
# modules:N procedures:M edges:E dynamicSkipped:D dynamicIncluded:X

# Bad scenario: invalid IR path or malformed JSON
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in tests/fixtures/ir/invalid.json
# stderr: usage: Invalid IR JSON.
# exit code: 65
```

### Performance Smoke

- Script: `tools/perf-smoke.ps1`
- Purpose: quick timing/memory snapshot for IR→Diagram conversion.
- Usage:
  ```powershell
  pwsh ./tools/perf-smoke.ps1 -In tests/fixtures/vba/cross_module_calls -Mode callgraph -TimeoutMs 15000
  ```
- Output: writes IR/Diagram to `out/tmp`, prints elapsed ms for `vba2json` and `ir2diagram`, and shows the shell working set; can be wired in CI (see `.github/workflows/dotnet.yml`, job `perf-smoke`).

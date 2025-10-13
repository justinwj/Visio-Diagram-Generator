**CLI Quick Reference**

VDG.VBA.CLI
- `vba2json`: Parse exported VBA (`.bas/.cls/.frm`) → IR JSON v0.1
  - Usage: `dotnet run --project src/VDG.VBA.CLI -- vba2json --in <folder> [--glob <pattern> ...] [--out <ir.json>] [--project-name <name>] [--root <path>] [--infer-metrics]`
  - Exit codes: 0 OK; 65 invalid input (e.g., duplicate module names); 70 internal error

- `ir2diagram`: Convert IR JSON → Diagram JSON (Project Call Graph)
  - Usage: `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>] [--mode <callgraph|module-structure|module-callmap|event-wiring|proc-cfg>] [--include-unknown] [--timeout <ms>]`
  - Behavior: carries `code.*` metadata, skips `~unknown` targets by default; `--include-unknown` includes sentinel edges; `--timeout` aborts long runs.
  - Exit codes: 0 OK; 65 invalid input (bad args/IR); 70 internal error/timeout.

  ### Validation Options
  - `--strict-validate`: Enable stricter IR validation (fails on inconsistent or incomplete IR fields).
    - Enforces module/procedure presence, valid kinds, well‑formed `locs`, and consistent dynamic call metadata (e.g., `target == "~unknown"` must have `isDynamic = true`).
    - Recommended for production pipelines where IR quality assurance is critical.
    - Example: `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --strict-validate --in out/tmp/project.ir.json --out out/tmp/project.diagram.json`

- `render`: Convenience — Sources → IR → Diagram → VSDX (calls `VDG.CLI`)
  - Usage: `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out <diagram.vsdx> [--mode <callgraph|module-structure|module-callmap>] [--cli <VDG.CLI.exe>]`

VDG.CLI
- Usage: `VDG.CLI.exe [options] <input.diagram.json> <output.vsdx>`
- Key options: spacing/page/containers; routing stubs; diagnostics (`--diag-*`); `--diag-json [path]`.
- CI/Tests: set `VDG_SKIP_RUNNER=1` to skip Visio COM; a stub `.vsdx` is written and the process exits 0.

Schemas
- IR: `shared/Config/vbaIr.schema.json` (`irSchemaVersion: 0.1`)
- Diagram: `shared/Config/diagramConfig.schema.json` (`schemaVersion: 1.2`)

Examples
- Sources → IR: `dotnet run --project src/VDG.VBA.CLI -- vba2json --in tests/fixtures/vba/cross_module_calls --out out/tmp/ir.json`
- IR → Diagram: `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir.json --out out/tmp/diag.json --mode callgraph`
- Render (smoke): `$env:VDG_SKIP_RUNNER=1; .\src\VDG.CLI\bin\Release\net48\VDG.CLI.exe out\tmp\diag.json out\tmp\smoke.vsdx`

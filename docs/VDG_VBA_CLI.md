# VDG VBA CLI

Purpose
- Reusable CLI for converting VBA sources → IR JSON (vba2json) and IR JSON → Diagram JSON (ir2diagram), suitable for automation and piping into `VDG.CLI`.

Commands
- vba2json: Parse exported VBA files and emit IR JSON v0.1
  - Usage:
    - `dotnet run --project src/VDG.VBA.CLI -- vba2json --in <folder> [--out <ir.json>] [--project-name <name>]`
  - Inputs: folder containing `.bas/.cls/.frm`
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
- For robust parsing beyond the skeleton, enhancements will be tracked in a subsequent milestone (types/params/returns, more call patterns, VBIDE integration).
- Alias handling: `vba2json` now tracks simple `Set alias = ...` assignments, pulling return-type metadata (`Module.Function As Type`) so `worker.Factory().RunAll` resolves to `Helper.RunAll`; targets fall back to the qualifier type when return types are unknown.
- Inside `With` blocks, chained member calls (e.g., `.Factory().RunAll`) reuse the same inference pipeline, yielding both the intermediate call (`Worker.Factory`) and the resolved helper (`Helper.RunAll`).
- Limitations: alias inference skips expressions with inline comments or member chains that lose type info, and dynamic invocation (`CallByName`, `Application.Run`) still emits `~unknown` targets.
- Return type lookups rely on explicit `As Type` in the signature; late-bound factories or Property Let/Set cannot yet feed alias inference.
- CFG mode (`--mode proc-cfg`) emits decision (`#dec`) and loop (`#loop`) scaffolds, and now surfaces combined loop-with-branch patterns (see `tests/fixtures/vba/cfg_nested`), but deeper nested merging remains on the roadmap.


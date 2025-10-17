**VDG MILESTONE EIGHT - IR->Diagram Converter (Project Call Graph)**

Goal
- [x] Convert VBA IR v0.1 into Diagram JSON for the project call graph, with stable mapping, lanes by artifact type, and rich metadata to power navigation and diagnostics.

Scope
- [x] CLI `ir2diagram` command reads IR (`shared/Config/vbaIr.schema.json`) and emits Diagram JSON (schemaVersion `1.2`).
- [x] Mapping for first mode (Project Call Graph):
  - Node = `Module.Proc`; label = `Module.Proc` (or `Proc` when viewing within container); container = Module; tier by module kind â†’ `Forms | Classes | Modules`.
  - Edge = `call` for each resolved `procedure.calls[]` item.
  - Deterministic ordering: modules by `name` (then `id`), procedures by `name` (then `id`).
- [x] Lanes by artifact type: map module `kind` â†’ `Forms | Classes | Modules`.
- [x] Metadata carried:
  - Nodes: `code.module`, `code.proc`, `code.kind`, `code.access`, `code.locs.file`, `code.locs.startLine`, `code.locs.endLine`.
  - Edges: `code.edge = call`, `code.site.module|file|line` from `call.site`, optional `code.branch` when present, and `code.dynamic=true` when `isDynamic`.
- [x] Defaults in the Diagram JSON envelope:
  - `layout.tiers = ["Forms","Classes","Modules"]`.
  - `layout.page = { heightIn: 8.5, marginIn: 0.5 }`.
  - `layout.spacing = { horizontal: 1.2, vertical: 0.6 }`.
  - `schemaVersion = "1.2"`.
- [x] Unknown dynamic targets: skip edges where `call.target == "~unknown"` (log/debug counter only; revisit in later milestone for stubs/annotations).

**Complexity & Performance**
- [x] Performance benchmarks for large projects (500+ procedures).
  - Harness captured in docs/perf/IR2Diagram_PerfPlan.md with presets for regression and large-scale inputs.
  - Synthetic benchmark fixture (`benchmarks/vba/massive_callgraph`, 24 modules / 600 procedures) with refreshed tooling (`tools/perf-smoke.ps1`, `tools/benchmarks/New-MassiveCallgraph.ps1`).
  - Latest run (JWJZENBOOK, .NET 8, 2025-10-16): vba2json 2.0s, ir2diagram 2.0s, diagram 449 KB, nodes/edges 600/600.
- [x] Memory usage profiling during IR->Diagram conversion.
  - Perf harness records working/private bytes; massive benchmark peaked at ~82 MB working set (Windows PowerShell 5.1).
  - Follow-up: integrate GC/allocation counters via dotnet-counters when available in CI.
- [x] Timeout/cancellation strategy for complex call graph analysis.

Out of Scope (this milestone)
- Advanced semantic grouping beyond `Forms|Classes|Modules`.
- Additional Diagram modes (Module Structure, Module Call Map, Event Wiring, Procedure CFG) - tracked separately.
- Enrichment of dynamic call inference beyond the current IR surface.

Related Docs & Schemas
- IR Spec and mapping guidance: `docs/VBA_IR.md`
- IR milestone context: `docs/VDG_MILESTONE_SIX.md`
- IR JSON Schema (v0.1): `shared/Config/vbaIr.schema.json`
- Diagram JSON Schema (1.2): `shared/Config/diagramConfig.schema.json`
- CLI reference and examples: `docs/VDG_VBA_CLI.md`
- IR governance/versioning: `docs/IR_Governance.md`

CLI
- `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>] [--mode callgraph] [--include-unknown] [--timeout <ms>] [--strict-validate]`
- End-to-end helper: `render` combines `vba2json + ir2diagram + VDG.CLI`.
  - `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out <diagram.vsdx> [--mode callgraph] [--cli <VDG.CLI.exe>]`

CLI Enhancements (UX)
- [x] `--include-unknown` flag to optionally include edges for dynamic unknown targets (e.g., dashed to a sentinel node) for debugging.
- [x] Progress reporting for large IR conversions (periodic counters: modules processed, procedures visited, edges emitted).
- [x] Help text: enrich `--help` with examples and option descriptions; add quick-start for `render`.
- [x] Validation warnings for suspicious call patterns (e.g., self-calls, high fan-out hot spots) surfaced as non-fatal notices.

Artifacts
- [x] `src/VDG.VBA.CLI/Program.cs`: finalize `ir2diagram` callgraph mapping and edge metadata (`code.dynamic`).
- [x] `docs/VBA_IR.md`: ensure Mapping section references dynamic flag propagation (callgraph mode).
- [x] `docs/VDG_VBA_CLI.md`: confirm examples for `ir2diagram` and `render` include callgraph mode.
- [x] Sample output: `samples/vba_callgraph.diagram.json` generated from `tests/fixtures/vba/cross_module_calls` (schema validated).
  - Captures node metadata (`code.module`, `code.proc`, `code.locs.*`) and call-site summaries used across docs/tests.

Integration & Validation
- [x] Validate generated Diagram JSON against `shared/Config/diagramConfig.schema.json` (schema 1.2).
- [x] Round-trip sanity: ensure output renders with current `VDG.CLI` (no schema/feature drift).
- [x] Smoke coverage that M5 diagnostics run cleanly against generated Diagrams (lane/page crowding metrics computed as expected).
  - Covered by ParserSmokeTests.M5DiagnosticsSmoke_EmitsMetricsAndIssues (VDG_SKIP_RUNNER, diagnostics JSON metrics/issues).
- [x] Establish performance baseline for end-to-end render pipeline (IR â†’ Diagram â†’ VSDX) on medium/large inputs.

Tests
- [x] Cross-module calls appear in edges; edge metadata carries call site:
  - Source: `Module1.Caller` â†’ Target: `Module2.Work`; `edges[].metadata.code.site.*` populated.
- [x] Node metadata mirrors IR:
  - `code.module`, `code.proc`, and `code.locs.*` present; access/kind when available.
- [x] Dynamic calls handling:
  - Unknown dynamic (`target = "~unknown"`) produces no edge and does not throw.
  - If a dynamic call has a concrete `target`, include the edge with `metadata.code.dynamic = true`.
- [x] Stable ordering for nodes/edges across runs (modules/procedures sorted as specified).
- [x] Summary metrics printed: total modules/procedures, edges emitted, dynamic calls skipped (and included, when `--include-unknown`).
- [x] Integration tests: `ir2diagram` output validates against Diagram schema; `VDG.CLI` consumes the output without errors.
- [x] Malformed IR inputs produce descriptive errors and exit code 65 (invalid input), not crashes.

Acceptance Criteria
- [ ] `ir2diagram --mode callgraph` produces schema-valid Diagram JSON (`schemaVersion: 1.2`).
- [ ] Nodes and edges contain the specified `code.*` metadata; call-site info is preserved.
- [ ] Lanes reflect module kinds; containers present for each module.
- [ ] Dynamic call metadata (`code.dynamic`) present when applicable; unknown dynamic calls do not render edges.
- [ ] End-to-end `render` produces a `.vsdx` for fixtures without errors.
- [ ] Diagram JSON validates against schema 1.2; current `VDG.CLI` renders without feature gaps.
- [ ] Summary metrics report dynamic-call skip counts; `--include-unknown` toggles inclusion behavior.
- [ ] Malformed IR yields a clear error message and exit code 65.

Implementation Notes
- Use `Forms|Classes|Modules` tier order; compute `tier` from module `kind`.
- Prefer deterministic order to enable stable diffs and reliable tests.
- Keep optional fields omitted rather than set to null in emitted JSON.
- For now, omit edges with `target == "~unknown"` to avoid orphan nodes; consider future visualization (e.g., dashed edges to a sentinel) if useful.

Dynamic Calls
- Skipped edges should be counted and surfaced in a summary (stdout) so users understand missing relationships.
- When `--include-unknown` is set, either:
  - emit edges to a sentinel node `~unknown` with dashed style and `metadata.code.dynamic=true`, or
  - include in summary metrics only (configurable; start with sentinel approach for visibility).
 - Reference: see `docs/VBA_IR.md` (Mapping â†’ Dynamic calls and unknown targets) for canonical field mapping and include-unknown behavior.

Container IDs & Collisions
- Container IDs derive from IR `module.id`; labels from `module.name`.
- If IR duplicates exist (should be prevented upstream), surface a descriptive error and refuse to proceed; do not invent container IDs here.
- When names are similar but IDs are unique, containers remain distinct; metadata should carry both `id` and `label` to avoid confusion.

Metadata Completeness & Fallbacks
- Validate presence of `code.module` and `code.proc` on nodes; if missing, fall back to `module.id`/`proc.id`.
- Only emit `code.access`, `code.kind`, and `code.locs.*` when present in IR; avoid nulls.
- Add a pre-emit validation step that warns when required `code.*` metadata is missing for many nodes (possible upstream IR issue).

Usage Examples
```powershell
# IR â†’ Diagram JSON (callgraph)
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json --mode callgraph

# One-step render from sources
dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/cross.vsdx --mode callgraph
```

Risks & Mitigations
- Dynamic calls may be frequent in some projects â†’ record `code.dynamic` and call-site to support later resolution; keep edges out for `~unknown` to reduce noise.
- Large projects may create dense call graphs â†’ rely on existing M3/M5 diagnostics to tune spacing/pagination; provide module-level aggregations in a later mode.
- Module name collisions â†’ defer to IR generatorâ€™s stable `id` policy; containers use module `id` and `name`.

Next Steps
- Surface additional modes: Module Call Map, Event Wiring, Procedure CFG (tracked separately) and experiment with semantic grouping within tiers.


**Acceptance Criteria**
- Harness captures perf + memory metrics via `tools/perf-smoke.ps1` for at least 500 procedures.
- `vba2json` and `ir2diagram` each complete <= 2.5s on the massive benchmark (24 modules / 600 procedures).
- Working set remains <= 120 MB during callgraph generation on the massive benchmark.
- Diagram output validates (schema + VDG.CLI run) with diagnostics limited to known crowding notices.

Bad Scenarios (expected behavior)
```powershell
# 1) Invalid IR file (malformed JSON or wrong shape)
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in tests/fixtures/ir/invalid.json
# stderr: "usage: Invalid IR JSON." (or schema mismatch diagnostic)
# exit code: 65 (invalid input)

# 2) Module name collision detected during extraction
dotnet run --project src/VDG.VBA.CLI -- vba2json --in path/to/fixtures/with_duplicate_modules
# stderr: "Duplicate module name detected: 'Module1' (IDs: Module1, Module1 (2))"
# exit code: 65 (invalid input)

# 3) Unknown dynamic calls present in IR (skipped by default)
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_with_dynamic.json --out out/tmp/diag.json
# stdout (summary): modules:N procedures:M edges:E dynamicSkipped:D
# Use --include-unknown to visualize dynamic calls to '~unknown'
```

Expected Outputs
- Diagram JSON with `nodes/edges/containers` and `layout.tiers = ["Forms","Classes","Modules"]`.
- Edges have `metadata.code.edge = "call"` and call-site fields; dynamic edges include `metadata.code.dynamic = true`.
- Summary line: `modules:N procedures:M edges:E dynamicSkipped:D dynamicIncluded:X progressEmits:Y progressLastMs:Z` (progress metrics always present; `dynamicIncluded` reflects `--include-unknown`).

Troubleshooting
- Schema errors: validate against `shared/Config/diagramConfig.schema.json`; check for missing required `nodes[].id/label`.
- Missing edges: inspect summary metrics; enable `--include-unknown` to visualize dynamic calls.
- Large inputs: consider running with pagination-friendly VDG options and monitor memory via `DOTNET_GCHeapHardLimit` or external profilers.

Versioning & Compatibility
- IR schema evolution: accept IR v0.1 and ignore unknown fields; document migration when IR minor bumps appear.
- Backward compatibility: fail fast with a clear message if a breaking IR major version is detected.
- CI/CD: add schema validation and end-to-end render smoke tests to catch regressions early.

Testing Matrix

| Case / Artifact | Expectation | Coverage |
|---|---|---|
| Module kinds (Module/Class/Form) | Tier mapping â†’ Forms/Classes/Modules; containers by module | Covered by tests/fixtures (`events_and_forms`, `cross_module_calls`) |
| Procedure kinds (Sub/Function/PropertyGet/Let/Set) | Nodes with `code.kind` and IDs `Module.Proc` | Covered for Sub/Function; Property* partially covered (add targeted fixture) |
| Cross-module calls | `edges[].metadata.code.edge = "call"`, site copied | Covered by `ParserSmokeTests.CrossModuleCalls_Appear_In_Callgraph_Diagram` |
| Dynamic calls (unknown targets) | Skipped by default; summary counts; `--include-unknown` renders sentinel edges | Covered by code; tests planned (fixture with `CallByName`/`Application.Run`) |
| Dynamic calls (known targets) | Edge emitted with `metadata.code.dynamic = true` | Covered by code/tests |
| Branch tags on calls (`then/else/loop`) | Edge `metadata.code.branch` when IR provides it | Covered (callgraph assertion added) |
| Container collisions (duplicate module names) | Descriptive error; exit 65 | Covered by tests (temp duplicate modules) |
| Missing `code.*` fields (locs/access) | Omit optional fields; warn when many missing | Covered by code; warn-level checks planned |
| Schema conformance (diagram 1.2) | Output validates against schema | Covered by integration test + CI |
| Large project performance | Summary metrics; no OOM; optional timeout/cancel | Benchmark planned |

CI Integration
- Diagram schema validation step on Ubuntu using `tools/diagram-validate.ps1` (schema 1.2).
- Windows render smoke using `VDG_SKIP_RUNNER` to avoid COM, asserts clean exit and stub output.
- Perf smoke job emits structured metrics to `out/perf/perf.json` and publishes a job summary (timings, node/edge counts, dynamic skip/include counts).
- Validation matrix runs `ir2diagram` in both default and `--strict-validate` modes; strict mode also asserts a crafted bad IR fails as expected.









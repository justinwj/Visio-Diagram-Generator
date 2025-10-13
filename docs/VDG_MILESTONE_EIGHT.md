**VDG MILESTONE EIGHT - IR → Diagram Converter (Project Call Graph)**

Goal
- [ ] Convert VBA IR v0.1 into Diagram JSON for the project call graph, with stable mapping, lanes by artifact type, and rich metadata to power navigation and diagnostics.

Scope
- [ ] CLI `ir2diagram` command reads IR (`shared/Config/vbaIr.schema.json`) and emits Diagram JSON (schemaVersion `1.2`).
- [ ] Mapping for first mode (Project Call Graph):
  - Node = `Module.Proc`; label = `Module.Proc` (or `Proc` when viewing within container); container = Module; tier by module kind → `Forms | Classes | Modules`.
  - Edge = `call` for each resolved `procedure.calls[]` item.
  - Deterministic ordering: modules by `name` (then `id`), procedures by `name` (then `id`).
- [ ] Lanes by artifact type: map module `kind` → `Forms | Classes | Modules`.
- [ ] Metadata carried:
  - Nodes: `code.module`, `code.proc`, `code.kind`, `code.access`, `code.locs.file`, `code.locs.startLine`, `code.locs.endLine`.
  - Edges: `code.edge = call`, `code.site.module|file|line` from `call.site`, optional `code.branch` when present, and `code.dynamic=true` when `isDynamic`.
- [ ] Defaults in the Diagram JSON envelope:
  - `layout.tiers = ["Forms","Classes","Modules"]`.
  - `layout.page = { heightIn: 8.5, marginIn: 0.5 }`.
  - `layout.spacing = { horizontal: 1.2, vertical: 0.6 }`.
  - `schemaVersion = "1.2"`.
- [ ] Unknown dynamic targets: skip edges where `call.target == "~unknown"` (log/debug counter only; revisit in later milestone for stubs/annotations).

**Complexity & Performance**
- [ ] Performance benchmarks for large projects (500+ procedures).
- [ ] Memory usage profiling during IR → Diagram conversion.
- [ ] Timeout/cancellation strategy for complex call graph analysis.

Out of Scope (this milestone)
- Advanced semantic grouping beyond `Forms|Classes|Modules`.
- Additional diagram modes (Module Structure, Module Call Map, Event Wiring, Procedure CFG) — tracked separately.
- Enrichment of dynamic call inference beyond the current IR surface.

CLI
- `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>] [--mode callgraph]`
- End-to-end helper: `render` combines `vba2json + ir2diagram + VDG.CLI`.
  - `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out <diagram.vsdx> [--mode callgraph] [--cli <VDG.CLI.exe>]`

CLI Enhancements (UX)
- [ ] `--include-unknown` flag to optionally include edges for dynamic unknown targets (e.g., dashed to a sentinel node) for debugging.
- [ ] Progress reporting for large IR conversions (periodic counters: modules processed, procedures visited, edges emitted).
- [ ] Help text: enrich `--help` with examples and option descriptions; add quick-start for `render`.
- [ ] Validation warnings for suspicious call patterns (e.g., self-calls, high fan-out hot spots) surfaced as non-fatal notices.

Artifacts
- [ ] `src/VDG.VBA.CLI/Program.cs`: finalize `ir2diagram` callgraph mapping and edge metadata (`code.dynamic`).
- [ ] `docs/VBA_IR.md`: ensure Mapping section references dynamic flag propagation (callgraph mode).
- [ ] `docs/VDG_VBA_CLI.md`: confirm examples for `ir2diagram` and `render` include callgraph mode.
- [ ] Sample output: `samples/vba_callgraph.diagram.json` generated from `tests/fixtures/vba/cross_module_calls`.

Integration & Validation
- [ ] Validate generated Diagram JSON against `shared/Config/diagramConfig.schema.json` (schema 1.2).
- [ ] Round-trip sanity: ensure output renders with current `VDG.CLI` (no schema/feature drift).
- [ ] Smoke coverage that M5 diagnostics run cleanly against generated diagrams (lane/page crowding metrics computed as expected).
- [ ] Establish performance baseline for end-to-end render pipeline (IR → Diagram → VSDX) on medium/large inputs.

Tests
- [ ] Cross-module calls appear in edges; edge metadata carries call site:
  - Source: `Module1.Caller` → Target: `Module2.Work`; `edges[].metadata.code.site.*` populated.
- [ ] Node metadata mirrors IR:
  - `code.module`, `code.proc`, and `code.locs.*` present; access/kind when available.
- [ ] Dynamic calls handling:
  - Unknown dynamic (`target = "~unknown"`) produces no edge and does not throw.
  - If a dynamic call has a concrete `target`, include the edge with `metadata.code.dynamic = true`.
- [ ] Stable ordering for nodes/edges across runs (modules/procedures sorted as specified).
- [ ] Summary metrics printed: total modules/procedures, edges emitted, dynamic calls skipped (and included, when `--include-unknown`).
- [ ] Integration tests: `ir2diagram` output validates against diagram schema; `VDG.CLI` consumes the output without errors.
- [ ] Malformed IR inputs produce descriptive errors and exit code 65 (invalid input), not crashes.

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
# IR → Diagram JSON (callgraph)
dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in out/tmp/ir_cross.json --out out/tmp/ir_cross.diagram.json --mode callgraph

# One-step render from sources
dotnet run --project src/VDG.VBA.CLI -- render --in tests/fixtures/vba/cross_module_calls --out out/tmp/cross.vsdx --mode callgraph
```

Risks & Mitigations
- Dynamic calls may be frequent in some projects → record `code.dynamic` and call-site to support later resolution; keep edges out for `~unknown` to reduce noise.
- Large projects may create dense call graphs → rely on existing M3/M5 diagnostics to tune spacing/pagination; provide module-level aggregations in a later mode.
- Module name collisions → defer to IR generator’s stable `id` policy; containers use module `id` and `name`.

Next Steps
- Surface additional modes: Module Call Map, Event Wiring, Procedure CFG (tracked separately) and experiment with semantic grouping within tiers.

Expected Outputs
- Diagram JSON with `nodes/edges/containers` and `layout.tiers = ["Forms","Classes","Modules"]`.
- Edges have `metadata.code.edge = "call"` and call-site fields; dynamic edges include `metadata.code.dynamic = true`.
- Summary line: `modules: N, procedures: M, edges: E, dynamicSkipped: D` (and `dynamicIncluded: X` when `--include-unknown`).

Troubleshooting
- Schema errors: validate against `shared/Config/diagramConfig.schema.json`; check for missing required `nodes[].id/label`.
- Missing edges: inspect summary metrics; enable `--include-unknown` to visualize dynamic calls.
- Large inputs: consider running with pagination-friendly VDG options and monitor memory via `DOTNET_GCHeapHardLimit` or external profilers.

Versioning & Compatibility
- IR schema evolution: accept IR v0.1 and ignore unknown fields; document migration when IR minor bumps appear.
- Backward compatibility: fail fast with a clear message if a breaking IR major version is detected.
- CI/CD: add schema validation and end-to-end render smoke tests to catch regressions early.

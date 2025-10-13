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

Out of Scope (this milestone)
- Advanced semantic grouping beyond `Forms|Classes|Modules`.
- Additional diagram modes (Module Structure, Module Call Map, Event Wiring, Procedure CFG) — tracked separately.
- Enrichment of dynamic call inference beyond the current IR surface.

CLI
- `dotnet run --project src/VDG.VBA.CLI -- ir2diagram --in <ir.json> [--out <diagram.json>] [--mode callgraph]`
- End-to-end helper: `render` combines `vba2json + ir2diagram + VDG.CLI`.
  - `dotnet run --project src/VDG.VBA.CLI -- render --in <folder> --out <diagram.vsdx> [--mode callgraph] [--cli <VDG.CLI.exe>]`

Artifacts
- [ ] `src/VDG.VBA.CLI/Program.cs`: finalize `ir2diagram` callgraph mapping and edge metadata (`code.dynamic`).
- [ ] `docs/VBA_IR.md`: ensure Mapping section references dynamic flag propagation (callgraph mode).
- [ ] `docs/VDG_VBA_CLI.md`: confirm examples for `ir2diagram` and `render` include callgraph mode.
- [ ] Sample output: `samples/vba_callgraph.diagram.json` generated from `tests/fixtures/vba/cross_module_calls`.

Tests
- [ ] Cross-module calls appear in edges; edge metadata carries call site:
  - Source: `Module1.Caller` → Target: `Module2.Work`; `edges[].metadata.code.site.*` populated.
- [ ] Node metadata mirrors IR:
  - `code.module`, `code.proc`, and `code.locs.*` present; access/kind when available.
- [ ] Dynamic calls handling:
  - Unknown dynamic (`target = "~unknown"`) produces no edge and does not throw.
  - If a dynamic call has a concrete `target`, include the edge with `metadata.code.dynamic = true`.
- [ ] Stable ordering for nodes/edges across runs (modules/procedures sorted as specified).

Acceptance Criteria
- [ ] `ir2diagram --mode callgraph` produces schema-valid Diagram JSON (`schemaVersion: 1.2`).
- [ ] Nodes and edges contain the specified `code.*` metadata; call-site info is preserved.
- [ ] Lanes reflect module kinds; containers present for each module.
- [ ] Dynamic call metadata (`code.dynamic`) present when applicable; unknown dynamic calls do not render edges.
- [ ] End-to-end `render` produces a `.vsdx` for fixtures without errors.

Implementation Notes
- Use `Forms|Classes|Modules` tier order; compute `tier` from module `kind`.
- Prefer deterministic order to enable stable diffs and reliable tests.
- Keep optional fields omitted rather than set to null in emitted JSON.
- For now, omit edges with `target == "~unknown"` to avoid orphan nodes; consider future visualization (e.g., dashed edges to a sentinel) if useful.

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


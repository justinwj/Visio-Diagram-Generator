**VDG MILESTONE NINE - Render Integration & Diagnostics**

Goal
- [ ] Deliver a seamless `ir2diagram -> VDG.CLI` pipeline that ships a Visio-ready callgraph with M5 diagnostics surfaced and documented.

Scope
- [ ] Pipeline integration: bundle `vba2json`, `ir2diagram`, and `VDG.CLI` into ready-to-copy commands (docs + smoke tests).
- [ ] Diagnostics surfacing: ensure M5 metrics (lane/page/container crowding, crossings, utilization) run on renderer output with actionable thresholds.
- [ ] Styling defaults: lock down callgraph tiers/formatting so rendered diagrams match published guidance.

Artifacts
- [ ] Update CLI docs with end-to-end render command, large-sheet hints, and diagnostic interpretation.
- [ ] Add a regression fixture (reuse `benchmarks/vba/massive_callgraph` or trimmed variant) for render smoke + diagnostics assertions.
- [ ] Acceptance criteria + checklist capturing diag expectations and render success cases.

Acceptance Criteria
- [ ] `dotnet run --project src/VDG.VBA.CLI -- render ...` produces a schema-valid Diagram and `.vsdx` for supported fixtures with no unexpected errors.
- [ ] M5 diagnostics emit lane/page/container crowding summaries; crossing/utilization metrics appear with documented thresholds.
- [ ] Default tiers/styling documented (modules/forms/classes) and reflected in rendered sample diagrams.
- [ ] CI smoke (VDG_SKIP_RUNNER=1) exercises end-to-end pipeline and validates diagnostics JSON.
- [ ] Docs updated with example commands, troubleshooting, and sheet-size guidance for large diagrams.

Open Questions
- Should we add additional tiers/columns for huge callgraphs before shipping the render story?
- Do we gate builds on specific diagnostic thresholds (e.g., fail when lane crowding > 95%) or treat them as warnings?

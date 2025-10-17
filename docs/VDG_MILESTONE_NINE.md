**VDG MILESTONE NINE - Render Integration & Diagnostics**

Goal
- [ ] Deliver a seamless `ir2diagram -> VDG.CLI` pipeline that ships a Visio-ready callgraph with M5 diagnostics surfaced and documented.

Status
- [x] Render helper pipeline (`vba2json -> ir2diagram -> VDG.CLI`) landed in Milestone Eight with smoke coverage.
- [x] M5 diagnostics metrics (lane/page/container crowding, crossings, utilization) already run in ParserSmokeTests.M5DiagnosticsSmoke_EmitsMetricsAndIssues.
- [x] Large synthetic benchmark fixture (`benchmarks/vba/massive_callgraph`) exists with perf harness scripts.
- [ ] Styling defaults for Visio layers/tiers need final polish and sign-off.
- [ ] Diagnostic threshold policy (warnings vs. errors) remains open.
- [ ] Documentation needs a single-step pipeline example and large-sheet guidance refresh.

Scope
- [ ] Pipeline integration: bundle `vba2json`, `ir2diagram`, and `VDG.CLI` into ready-to-copy commands (docs + smoke tests).
  - [x] `render` helper produces Diagram JSON and `.vsdx` outputs (Milestone Eight acceptance).
  - [ ] Finalize copy/paste command set + CI smoke orchestration in docs.
- [ ] Diagnostics surfacing: ensure M5 metrics (lane/page/container crowding, crossings, utilization) run on renderer output with actionable thresholds.
  - [x] Metrics emitted during existing smoke runs with documented JSON payloads.
  - [ ] Define severity thresholds and CLI/Docs messaging for each diagnostic (e.g., lane crowding warn >= 85%, error >= 95%).
  - [ ] Document default CI behavior (warn-only by default; opt-in failure when thresholds exceeded) and surface env overrides (`VDG_DIAG_LANE_WARN`, `VDG_DIAG_LANE_ERR`, etc.).
- [ ] Styling defaults: lock down callgraph tiers/formatting so rendered diagrams match published guidance.
  - [x] Baseline tiers `["Forms","Classes","Modules"]` established in `ir2diagram` output.
  - [ ] Visio styling pass (lane colors, container padding, legend) and documentation update.
  - [ ] Styling audit: finalize layer themes (lane color palette, container line weights, font stack), add legend asset placeholder (`docs/render_legend.png`), and validate branding consistency with refreshed sample `.vsdx`.

Artifacts
- [ ] Update CLI docs with end-to-end render command, large-sheet hints, and diagnostic interpretation.
  - [x] Render quick-start and option descriptions published in Milestone Eight (`docs/VDG_VBA_CLI.md`).
  - [ ] Expand docs with single-step pipeline example and diagnostic interpretation table.
  - [ ] Publish user-facing render quick start at `docs/rendering.md` covering the IR -> Diagram -> Visio flow.
- [ ] Add a regression fixture (reuse `benchmarks/vba/massive_callgraph` or trimmed variant) for render smoke + diagnostics assertions.
  - [x] Fixture and perf harness available under `benchmarks/vba/massive_callgraph`.
  - [ ] Add render smoke assertions + diagnostics baseline artifacts.
  - [ ] Ensure render smoke job captures diagnostic summary counts in `out/perf/render_diagnostics.json` and gate changes when metrics drift more than +/- 5% on identical inputs.
- [ ] Acceptance criteria + checklist capturing diag expectations and render success cases.

Acceptance Criteria
- [ ] `dotnet run --project src/VDG.VBA.CLI -- render ...` produces a schema-valid Diagram and `.vsdx` for supported fixtures with no unexpected errors.
  - [x] Verified during Milestone Eight end-to-end smoke runs.
  - [ ] Reconfirm after styling adjustments.
- [ ] M5 diagnostics emit lane/page/container crowding summaries; crossing/utilization metrics appear with documented thresholds.
  - [x] Metrics currently emitted and captured in smoke JSON outputs.
  - [ ] Document thresholds and decide warning/error behavior.
- [ ] Default tiers/styling documented (modules/forms/classes) and reflected in rendered sample diagrams.
  - [x] Baseline tiers documented in `ir2diagram` outputs.
  - [ ] Update Visio samples to reflect finalized default styling.
- [ ] CI smoke (VDG_SKIP_RUNNER=1) exercises end-to-end pipeline and validates diagnostics JSON.
  - [x] Existing smoke suite covers render + diagnostics validation.
  - [ ] Extend assertions for threshold severity handling.
- [ ] Docs updated with example commands, troubleshooting, and sheet-size guidance for large diagrams.
  - [x] Initial large-sheet hints and render quick-start published previously.
  - [ ] Add consolidated single-step pipeline example and troubleshooting refresh.

Open Questions
- Should we add additional tiers/columns for huge callgraphs before shipping the render story, or defer expanded tier groupings to Milestone Ten+?
- Do we gate builds on specific diagnostic thresholds (e.g., fail when lane crowding > 95%) or treat them as warnings (with validation deferred to Milestone Ten+)?

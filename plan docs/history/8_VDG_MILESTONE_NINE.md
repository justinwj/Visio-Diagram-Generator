**VDG MILESTONE NINE - Render Integration & Diagnostics**

_Internal planning doc (relocated to `plan docs/VDG_MILESTONE_NINE.md`)._

Goal
- [ ] Deliver a seamless `ir2diagram -> VDG.CLI` pipeline that ships a Visio-ready callgraph with M5 diagnostics surfaced and documented.

Status
- [x] Render helper pipeline (`vba2json -> ir2diagram -> VDG.CLI`) landed in Milestone Eight with smoke coverage.
- [x] M5 diagnostics metrics (lane/page/container crowding, crossings, utilization) already run in ParserSmokeTests.M5DiagnosticsSmoke_EmitsMetricsAndIssues.
- [x] Large synthetic benchmark fixture (`benchmarks/vba/massive_callgraph`) exists with perf harness scripts.
- [x] Styling defaults for Visio layers/tiers documented (`docs/StylingDefaults.md`) with legend + sample assets staged.
- [x] Diagnostic threshold policy documented: warn >=85%, error >=95%, page warn 90%, warn-only default with `VDG_DIAG_*` overrides.
- [x] Wrap-up polish tracked (sample refresh outstanding).

Scope
- [x] Pipeline integration: bundle `vba2json`, `ir2diagram`, and `VDG.CLI` into ready-to-copy commands (docs + smoke tests).
  - [x] `render` helper produces Diagram JSON and `.vsdx` outputs (Milestone Eight acceptance).
  - [x] Finalize copy/paste command set + CI smoke orchestration in docs/workflow (`docs/rendering.md`, `tools/render-smoke.ps1`, CI job updated).
- [x] Diagnostics surfacing: ensure M5 metrics (lane/page/container crowding, crossings, utilization) run on renderer output with actionable thresholds.
  - [x] Metrics emitted during existing smoke runs with documented JSON payloads.
  - [x] Define severity thresholds and CLI/Docs messaging (lane warn >=85%, lane error >=95%, page warn >=90%).
  - [x] Document default CI behavior (warn-only by default; opt-in failure via `VDG_DIAG_FAIL_LEVEL`, crowding ratios configurable with `VDG_DIAG_LANE_WARN`, `VDG_DIAG_LANE_ERR`, `VDG_DIAG_PAGE_WARN`).
- [x] Styling defaults: lock down callgraph tiers/formatting so rendered diagrams match published guidance.
  - [x] Baseline tiers `["Forms","Sheets","Classes","Modules"]` established in `ir2diagram` output.
  - [x] Visio styling pass (lane colors, container padding, typography) published in `docs/StylingDefaults.md`.
  - [x] Styling audit: palette locked, legend asset at `docs/render_legend.png`, sample `.vsdx` staged at `samples/vba_callgraph_styled.vsdx`.

Artifacts
- [x] Update CLI docs with end-to-end render command, large-sheet hints, and diagnostic interpretation.
  - [x] Render quick-start and option descriptions published in Milestone Eight (`docs/VDG_VBA_CLI.md`).
  - [x] Expand docs with single-step pipeline example and diagnostic interpretation table.
  - [x] Publish user-facing render quick start at `docs/rendering.md` covering the IR -> Diagram -> Visio flow.
- [x] Add a regression fixture (reuse `benchmarks/vba/massive_callgraph` or trimmed variant) for render smoke + diagnostics assertions.
  - [x] Fixture and perf harness available under `benchmarks/vba/massive_callgraph`.
  - [x] Add render smoke assertions + diagnostics baseline artifacts (`tools/render-smoke.ps1`, `tests/baselines/render_diagnostics.json`).
  - [x] Ensure render smoke job captures diagnostic summary counts in `out/perf/render_diagnostics.json` and gate changes when metrics drift more than +/- 5% on identical inputs.
- [x] Styling packet: `docs/StylingDefaults.md`, `docs/render_legend.png`, and `samples/vba_callgraph_styled.vsdx` aligned with Milestone Nine defaults.
- [ ] Acceptance criteria + checklist capturing diag expectations and render success cases.

Acceptance Criteria
- [ ] `dotnet run --project src/VDG.VBA.CLI -- render ...` produces a schema-valid Diagram and `.vsdx` for supported fixtures with no unexpected errors.
  - [x] Verified during Milestone Eight end-to-end smoke runs.
  - [x] Reconfirm after styling adjustments (2025-10-17 render + smoke reruns logged via PowerShell 7.5.3).
- [ ] M5 diagnostics emit lane/page/container crowding summaries; crossing/utilization metrics appear with documented thresholds.
  - [x] Metrics currently emitted and captured in smoke JSON outputs.
  - [x] Threshold policy captured (lane warn >=85%, lane error >=95%, page warn >=90%), warn-only default with `VDG_DIAG_FAIL_LEVEL` opt-in for failures.
- [ ] Default tiers/styling documented (modules/forms/classes) and reflected in rendered sample diagrams.
  - [x] Baseline tiers documented in `ir2diagram` outputs.
- [ ] Update Visio samples to reflect finalized default styling.
- [x] CI smoke (VDG_SKIP_RUNNER=1) exercises end-to-end pipeline and validates diagnostics JSON.
  - [x] Existing smoke suite covers render + diagnostics validation.
  - [x] Extend assertions for threshold severity handling (`tools/render-smoke.ps1` fail-level gating check added 2025-10-17).
- [x] Docs updated with example commands, troubleshooting, and sheet-size guidance for large diagrams.
  - [x] Initial large-sheet hints and render quick-start published previously.
  - [x] Add consolidated single-step pipeline example and troubleshooting refresh (see `docs/rendering.md`).

Open Questions
- Should we add additional tiers/columns for huge callgraphs before shipping the render story, or defer expanded tier groupings to Milestone Ten+?
- When should CI flip `VDG_DIAG_FAIL_LEVEL` from warn-only to error gating (Milestone Ten validation story)?

Remaining Polish
- Refresh sample `.vsdx` exports so visuals reflect the final tier palette and legend (pending; palette automation deferred to Milestone Twelve).

Assessment
- Feature work is complete; the render pipeline, diagnostics thresholds, and styling defaults are locked.
- Remaining actions are carried forward into Milestone Ten (fixtures/tests) and Milestone Twelve (palette automation); milestone nine can be considered closed from a feature perspective (all smoke tests passing under PowerShell 7.5.3).

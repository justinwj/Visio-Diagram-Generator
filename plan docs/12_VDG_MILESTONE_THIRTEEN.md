**VDG MILESTONE XIII – Content Fidelity & Useful Diagrams**

Goal
- [ ] Make “view-mode” diagrams fully informative and actionable for developers: display all modules, forms, procedures, and call edges, with correct connector routing and visual layout.

Status Snapshot (Oct 25)
- [ ] View-mode cards & overflow badges render, but connector visualization is still unreliable; Visio output shows clumped nodes with few useful edges.
- [ ] Layout computation lives inside the C# runner—needs to migrate to the F# algorithms layer per the new high-level plan so paging/connector routing stay deterministic and testable.
- [ ] Diagnostics emit overall counts but do not yet prove the Visio page actually drew the routed polylines.
- [ ] Tests cover layout JSON but not Visio connector output; fixtures still show “hairball” routing or layout.

Primary Work Streams

### 1. Diagram Content & Layout
- [ ] Restore per-procedure node emission for callgraph/event-wiring/module-structure outputs in view mode.
- [ ] Re-enable connector generation/routing (call edges, event wiring, module references) with collision-aware layout.
- [ ] Render forms, classes, and modules with nested procedure shapes; ensure long forms like `frmItemSearch` expand to show members.
- [ ] Add `layout.outputMode=view` default (with `print` as alternate) and branch planner/layout heuristics accordingly.
- [ ] Re-tune lane sizing/spacing for view mode so internal nodes are visible without overflowing.
- [ ] Provide fallback visual cues (overflow badges, truncated lists) when a module exceeds the available space even in view mode.
- [ ] Extract `ComputeViewModeLayout`, paging, and connector planning into `VisioDiagramGenerator.Algorithms` (F#) so C# only renders pre-planned geometry and no longer recomputes layout per page.
- [ ] Feed connector polylines directly from the F# results into Visio (single-page + paged) to guarantee consistency between diagnostics and renderings.

### 2. Diagnostics & Instrumentation
- [ ] Extend diagnostics summary to report node/connector totals per mode (expected vs. rendered).
- [ ] Add warnings when procedures or connectors are dropped in view mode.
- [ ] Capture per-mode metrics in diagnostics JSON and CLI summary (view vs. print).
- [ ] Log per-page connector counts, routed polyline lengths, and skipped-edge reasons to make layout/Visio discrepancies obvious.
- [ ] Surface a “no empty forms/modules” assertion in diagnostics; fail fast if any tier card has zero visible children in view mode.

### 3. Testing & Fixtures
- [ ] Update CLI/unit tests to assert node/edge counts and connector presence for representative samples (`frmItemSearch`, `MouseScroll`, cross-module calls).
- [ ] Add regression tests that toggle `layout.outputMode` and validate both view and print planners behave as expected.
- [ ] Refresh fixture baselines (including `invSys`) with new view-mode outputs; update ledger/metadata.
- [ ] Ensure CI runs view-mode fixture verification (extend render-fixtures job or add dedicated step).
- [ ] Add contract tests around the F# layout planner to ensure identical IR/options always yield a stable `LayoutPlan`, `PagePlan`, and edge polyline set.
- [ ] Introduce Visio-level smoke tests (or diagram JSON assertions) that validate connectors per card/page after the render step when `VDG_SKIP_VISIO` is enabled.

### 4. Documentation & Tooling
- [ ] Document the new `outputMode` metadata/CLI flag in README and CLI help.
- [ ] Add guidance to `docs/PagingPlanner.md` and `FixtureGuide.md` describing how to regenerate view-mode artifacts.
- [ ] Provide troubleshooting notes for missing connectors/nodes (e.g., diagnostics warnings, metadata overrides).
- [ ] Capture the F#/C# responsibility split from `plan docs/high_level_plan.md` inside this milestone’s README/brief so future contributors route all layout math through the algorithms layer.
- [ ] Produce a “connector debugging” section that explains how to trace a routed edge from diagnostics → layout JSON → Visio polyline.

Acceptance Criteria
- Running the CLI with `layout.outputMode=view` produces diagrams where all modules, forms, and procedures (including `frmItemSearch`) are visible with connectors intact.
- Diagnostics/summary output matches expected node, connector, and edge counts for fixtures; any omissions raise warnings/errors.
- Test suite covers view-mode rendering, connector routing, and mode switching (view vs. print).
- CI fixture verification passes with updated view-mode artifacts.
- Layout/paging/connector computation happens in F#, emitting data consumed verbatim by C# with no ad-hoc re-layout during rendering.
- Per-page diagnostics confirm connectors were drawn (counts > 0, zero skipped edges) for the standard fixtures; failures block the milestone.

Backlog / Future Notes
- Print-mode separation (Milestone XIV) will finalize the dual-mode planner.
- Subsequent milestones (XV, XVI) will focus on usability polish, performance, and overall hardening once content fidelity is restored.

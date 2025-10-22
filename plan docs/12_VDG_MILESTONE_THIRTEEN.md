**VDG MILESTONE XIII – Content Fidelity & Useful Diagrams**

Goal
- Make “view-mode” diagrams fully informative and actionable for developers: display all modules, forms, procedures, and call edges, with correct connector routing and visual layout.

Primary Work Streams

### 1. Diagram Content & Layout
- [ ] Restore per-procedure node emission for callgraph/event-wiring/module-structure outputs in view mode.
- [ ] Re-enable connector generation/routing (call edges, event wiring, module references) with collision-aware layout.
- [ ] Render forms, classes, and modules with nested procedure shapes; ensure long forms like `frmItemSearch` expand to show members.
- [ ] Add `layout.outputMode=view` default (with `print` as alternate) and branch planner/layout heuristics accordingly.
- [ ] Re-tune lane sizing/spacing for view mode so internal nodes are visible without overflowing.
- [ ] Provide fallback visual cues (overflow badges, truncated lists) when a module exceeds the available space even in view mode.

### 2. Diagnostics & Instrumentation
- [ ] Extend diagnostics summary to report node/connector totals per mode (expected vs. rendered).
- [ ] Add warnings when procedures or connectors are dropped in view mode.
- [ ] Capture per-mode metrics in diagnostics JSON and CLI summary (view vs. print).

### 3. Testing & Fixtures
- [ ] Update CLI/unit tests to assert node/edge counts and connector presence for representative samples (`frmItemSearch`, `MouseScroll`, cross-module calls).
- [ ] Add regression tests that toggle `layout.outputMode` and validate both view and print planners behave as expected.
- [ ] Refresh fixture baselines (including `invSys`) with new view-mode outputs; update ledger/metadata.
- [ ] Ensure CI runs view-mode fixture verification (extend render-fixtures job or add dedicated step).

### 4. Documentation & Tooling
- [ ] Document the new `outputMode` metadata/CLI flag in README and CLI help.
- [ ] Add guidance to `docs/PagingPlanner.md` and `FixtureGuide.md` describing how to regenerate view-mode artifacts.
- [ ] Provide troubleshooting notes for missing connectors/nodes (e.g., diagnostics warnings, metadata overrides).

Acceptance Criteria
- Running the CLI with `layout.outputMode=view` produces diagrams where all modules, forms, and procedures (including `frmItemSearch`) are visible with connectors intact.
- Diagnostics/summary output matches expected node, connector, and edge counts for fixtures; any omissions raise warnings/errors.
- Test suite covers view-mode rendering, connector routing, and mode switching (view vs. print).
- CI fixture verification passes with updated view-mode artifacts.

Backlog / Future Notes
- Print-mode separation (Milestone XIV) will finalize the dual-mode planner.
- Subsequent milestones (XV, XVI) will focus on usability polish, performance, and overall hardening once content fidelity is restored.

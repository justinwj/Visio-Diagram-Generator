# Render Error-Handling & Fallbacks

Multi-page renders intentionally prefer *degraded success* over hard failure so
large VBA datasets keep producing artifacts while still surfacing actionable
diagnostics. The policy below explains how the CLI classifies results, which
signals to watch, and the supported fallback levers when mitigation is applied.

## Result Categories
- **Fatal failure (non-zero exit code).** Parsing issues, Visio COM problems, or
  explicit diagnostics gating (`VDG_DIAG_FAIL_LEVEL=warning|error`) stop the run.
- **Mitigated success (`partialRender=yes`).** The planner kept rendering but
  logged degradations such as lane/page overflow, truncated nodes, or skipped
  connectors. The process exits `0` so automation can continue, but warnings are
  emitted on stdout/stderr and in diagnostics JSON.
- **Clean success.** No mitigations triggered; the summary line omits
  `partialRender` and per-page logs stay informational.

## Runtime Signals
- Planner summary adds `partialRender=yes` plus overflow/crowding counters. The
  CLI now prints an explicit warning that points back to this document.
- Per-page diagnostics list `partial=yes`, connector limits, truncation counts,
  and lane warnings. These are mirrored in `metrics.pages[]` inside the
  diagnostics JSON.
- `tools/render-smoke.ps1` and `tools/render-fixture.ps1` capture the same
  metrics; both scripts surface skipped connector counts on the console.

## When Partial Renders Occur
- **Lane/page crowding:** Occupancy exceeds tuned caps. When multiple pages or
  lane splitting is available the severity is downgraded to warning, but the
  run is marked partial.
- **Truncated nodes:** A single card cannot fit the usable page height.
- **Skipped modules/connectors:** Filtering (`--modules`, `--max-pages`) or
  Visio connector limits prevent full output.
- **Overflow mitigation:** Planner moved content to extra pages or dropped
  connectors to keep Visio stable.

## Fallback Heuristics
1. **Increase page real estate.** Use `--page-width`, `--page-height`, and
   `--page-margin` (or override `layout.page`) to give the planner more room.
2. **Adjust spacing.** Tighten `--spacing-v` / `layout.spacing.vertical` when
  lanes are close to the limit; expand spacing when connectors are tangled.
3. **Split the dataset.** Combine `--modules`, `--modules include/exclude`, or
  `--max-pages` to render targeted slices. The CLI logs skipped modules so you
  can confirm coverage.
4. **Inspect diagnostics JSON.** `metrics.pages[].skippedConnectors`,
   `metrics.truncatedNodeCount`, and `issues[]` explain which pages degraded and
   why. Fixtures keep deterministic hashes so drift becomes obvious.
5. **Escalate to hard failures when ready.** Set `VDG_DIAG_FAIL_LEVEL=warning`
   (or `error`) in CI to make partial renders fail the build once the team is
   confident in the thresholds.

## Workflow Tips
- `tools/render-smoke.ps1 -UseVisio` runs the end-to-end smoke with Visio COM,
  enabling you to validate mitigation behaviour locally or on a Visio-equipped
  runner.
- Regenerate the `invSys` fixture (`tools/render-fixture.ps1 -FixtureName invSys`)
  after layout tweaks; the script logs skipped connectors and appends the ledger
  entry so the drift guardrail stays intact.
- Always review the console warning when `partialRender=yes` appears. It
  indicates mitigations were applied even if the exit code is `0`.

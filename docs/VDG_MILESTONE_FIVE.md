**VDG MILESTONE FIVE PLAN**

Goal
- [x] Emit actionable layout diagnostics (e.g., tier overcrowded warnings) so we can iterate without guessing.

Scope
- [x] Tier crowding metrics per page: occupancy ratio and node count; warn/error when over thresholds.
- [ ] Page/band crowding: usable height vs. required height; warn when near/over limits (soft vs. hard thresholds).
- [ ] Crossing density and corridor utilization severities (build on existing M3 metrics): escalate when above thresholds.
- [ ] Container crowding: occupancy within explicit containers (if present), overflow detection + ratios.
- [x] Structured diagnostics output (JSON) for CI dashboards (initial payload).

Schema Additions (backward compatible)
- [x] `layout.diagnostics.level`: `"info" | "warning" | "error"` (min level to emit; default `info`).
- [x] `layout.diagnostics.laneCrowdWarnRatio`: number in (0,1], default `0.85`.
- [x] `layout.diagnostics.laneCrowdErrorRatio`: number in (0,1], default `0.95`.
- [x] `layout.diagnostics.pageCrowdWarnRatio`: number in (0,1], default `0.90`.
- [ ] `layout.diagnostics.crossingsWarn`: integer; warn when planned route crossings exceed this (default `200`).
- [ ] `layout.diagnostics.crossingsError`: integer; error above this (default `400`).
- [ ] `layout.diagnostics.utilizationWarnMin`: corridor utilization percent (0–100), default `40`.
- [x] `layout.diagnostics.emitJson`: boolean (default `false`).
- [x] `layout.diagnostics.jsonPath`: string (optional output path override).

CLI Flags
- [x] `--diag-level <info|warning|error>`
- [x] `--diag-lane-warn <ratio>`
- [x] `--diag-lane-error <ratio>`
- [x] `--diag-page-warn <ratio>`
- [ ] `--diag-cross-warn <n>` / `--diag-cross-err <n>`
- [ ] `--diag-util-warn <percent>`
- [x] `--diag-json [path]` (enable JSON output; optional path override)

Diagnostics Emitted
- [x] Tier overcrowded (per page): `lane='<name>' page=<n> occupancy=<r%> nodes=<k> warn|error`.
- [ ] Page band crowding: usable `<u>in`, required `<r>in`, top offenders by height.
- [ ] Crossing density: `planned=<n>` severity based on thresholds; include short suggestion list (increase channels gap, adjust tiers).
- [ ] Corridor utilization: percent of cross‑lane edges using corridors; warn when below configured minimum.
- [ ] Container crowding: per container occupancy; warn when `> laneCrowdWarnRatio`.
- [x] JSON document (if enabled): `{ version, metrics: { connectorCount, straightLineCrossings, pageHeight, usableHeight, lanePages[] }, issues[] }`.

Implementation Notes
- [x] Compute occupancy using `LayoutResult` nodes per page: `sum(heights) + gaps` divided by `usableHeight`.
- [x] Respect precedence: CLI overrides > JSON > defaults.
- [x] Keep console lines stable: prefix with `info:` / `warning:` / `error:`.
- [x] If `emitJson=true` or `--diag-json`, write diagnostics JSON to provided path or default `out/diagnostics.json`.

Deliverables
- [x] Schema updates for new diagnostics knobs.
- [ ] README updates for new diagnostics knobs.
- [x] CLI: thresholds + JSON emission; stable console messages.
- [x] Tests: lane crowding detection (warn/error).
- [ ] Tests: JSON shape and contents, CLI overrides precedence.
- [x] Samples: a crowded tiers example that reliably triggers warnings.

Acceptance Criteria
- [x] Running the CLI on a dense diagram prints tier crowding warnings with occupancy ratios.
- [x] Optional JSON file contains `metrics` and `issues[]` aligned with console output.
- [x] Thresholds configurable via JSON and CLI with documented precedence.
- [x] Backward compatibility preserved.

Recommended Sequence
- [x] Schema + CLI flags for diagnostics thresholds and JSON output.
- [x] Implement occupancy computation + per‑page crowding.
- [ ] Hook crossings/utilization thresholds into existing M3 diagnostics.
- [x] JSON writer + file path resolution.
- [x] Samples + tests; README pending.

Usage
- Example run (writes JSON to default path):
  - `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" --diag-json --diag-lane-warn 0.80 --diag-lane-error 0.90 --diag-page-warn 0.90 "samples\m5_dense_tier.json" "out\m5_dense_tier.vsdx"`
- JSON output fields:
  - `metrics.connectorCount`, `metrics.straightLineCrossings`, `metrics.pageHeight`, `metrics.usableHeight`, `metrics.lanePages[] { tier, page, occupancyRatio, nodes }`
  - `issues[] { code, level, message, lane, page }`

**VDG MILESTONE FIVE PLAN**

Goal
- [x] Emit actionable layout diagnostics (e.g., tier overcrowded warnings) so we can iterate without guessing.

Scope
- [x] Tier crowding metrics per page: occupancy ratio and node count; warn/error when over thresholds.
- [x] Page/band crowding: usable height vs. required height; warn when near/over limits (warn>=pageCrowdWarnRatio, error when >100%).
- [x] Crossing density and corridor utilization severities (build on existing M3 metrics): escalate when above thresholds.
- [x] Container crowding: occupancy within explicit containers (if present), overflow detection + ratios.
- [x] Structured diagnostics output (JSON) for CI dashboards (initial payload).

Schema Additions (backward compatible)
- [x] `layout.diagnostics.level`: `"info" | "warning" | "error"` (min level to emit; default `info`).
- [x] `layout.diagnostics.laneCrowdWarnRatio`: number in (0,1], default `0.85`.
- [x] `layout.diagnostics.laneCrowdErrorRatio`: number in (0,1], default `0.95`.
- [x] `layout.diagnostics.pageCrowdWarnRatio`: number in (0,1], default `0.90`.
- [x] `layout.diagnostics.crossingsWarn`: integer; warn when planned route crossings exceed this (default `200`).
- [x] `layout.diagnostics.crossingsError`: integer; error above this (default `400`).
- [x] `layout.diagnostics.utilizationWarnMin`: corridor utilization percent (0–100), default `40`.
- [x] `layout.diagnostics.emitJson`: boolean (default `false`).
- [x] `layout.diagnostics.jsonPath`: string (optional output path override).

CLI Flags
- [x] `--diag-level <info|warning|error>`
- [x] `--diag-lane-warn <ratio>`
- [x] `--diag-lane-error <ratio>`
- [x] `--diag-page-warn <ratio>`
- [x] `--diag-cross-warn <n>` / `--diag-cross-err <n>`
- [x] `--diag-util-warn <percent>`
- [x] `--diag-json [path]` (enable JSON output; optional path override)

Diagnostics Emitted
- [x] Tier overcrowded (per page): `lane='<name>' page=<n> occupancy=<r%> nodes=<k> warn|error`.
- [x] Page band crowding: warn when max lane occupancy on a page >= `pageCrowdWarnRatio`; error when > 100%; includes top offenders by height in message.
- [x] Crossing density: `planned=<n>` severity based on thresholds; include short suggestion list (increase channels gap, adjust tiers).
- [x] Corridor utilization: percent of cross‑lane edges using corridors; warn when below configured minimum.
- [x] Container crowding: per container occupancy; warn/error at lane thresholds; JSON issues emitted.
- [x] JSON document (if enabled): `{ version, metrics: { connectorCount, straightLineCrossings, pageHeight, usableHeight, lanePages[] }, issues[] }`.

Implementation Notes
- [x] Compute occupancy using `LayoutResult` nodes per page: `sum(heights) + gaps` divided by `usableHeight`.
- [x] Respect precedence: CLI overrides > JSON > defaults.
- [x] Keep console lines stable: prefix with `info:` / `warning:` / `error:`.
- [x] If `emitJson=true` or `--diag-json`, write diagnostics JSON to provided path or default `<output>.diagnostics.json`.
- [x] Honor `layout.diagnostics.level` (and `--diag-level`) for console and JSON issues.
- [x] Compute container occupancy ratios (explicit or inferred bounds) per page; emit `ContainerOverflow` and `ContainerCrowding` when applicable; include `metrics.containers[]`.
  - Occupancy = `sum(member heights on page) + (count-1) * verticalSpacing` divided by usable page height.
  - Usable = `pageHeight - 2*margin - titleHeight` (titleHeight = 0.6in when title present; else 0).
  - Severity uses lane thresholds: warn at `laneCrowdWarnRatio` (0.85), error at `laneCrowdErrorRatio` (0.95).

Deliverables
- [x] Schema updates for new diagnostics knobs.
- [x] README updates for new diagnostics knobs.
- [x] CLI: thresholds + JSON emission; stable console messages.
- [x] Tests: lane crowding detection (warn/error).
- [x] Tests: JSON shape and contents, CLI overrides precedence.
- [x] Samples: a crowded tiers example that reliably triggers warnings.

Acceptance Criteria
- [x] Running the CLI on a dense diagram prints tier crowding warnings with occupancy ratios.
- [x] Optional JSON file contains `metrics` and `issues[]` aligned with console output.
- [x] Thresholds configurable via JSON and CLI with documented precedence.
- [x] Backward compatibility preserved.

Recommended Sequence
- [x] Schema + CLI flags for diagnostics thresholds and JSON output.
- [x] Implement occupancy computation + per‑page crowding.
- [x] Hook crossings/utilization thresholds into existing M3 diagnostics.
- [x] JSON writer + file path resolution.
- [x] Samples + tests; README pending.

Usage
- Example run (writes JSON to default path):
  - `& "src\VDG.CLI\bin\Debug\net48\VDG.CLI.exe" --diag-json --diag-lane-warn 0.80 --diag-lane-error 0.90 --diag-page-warn 0.90 "samples\m5_dense_tier.json" "out\m5_dense_tier.vsdx"`
- JSON output fields:
  - `metrics.connectorCount`, `metrics.straightLineCrossings`, `metrics.pageHeight`, `metrics.usableHeight`
  - `metrics.lanePages[] { tier, page, occupancyRatio, nodes }`
  - `metrics.containers[] { id, tier, page, occupancyRatio, nodes }`
- `issues[] { code, level, message, lane, page }` (filtered by `diag-level`; includes `ContainerOverflow` and `ContainerCrowding`)

- Example with containers (writes `<output>.diagnostics.json`):
  - `& "src\\VDG.CLI\\bin\\Debug\\net48\\VDG.CLI.exe" --diag-level info --diag-json "samples\\m4_containers_sample.json" "out\\m4_containers_sample.vsdx"`
  - Diagnostics JSON (excerpt):
    ```json
    {
      "metrics": {
        "containers": [
          { "id": "Z_SVC", "tier": "Services", "page": 1, "occupancyRatio": 0.42, "nodes": 3 }
        ]
      },
      "issues": [
        { "code": "ContainerOverflow", "level": "warning", "lane": "Services", "page": 1, "message": "sub-container 'Z_SVC' overflows lane 'Services'." }
      ]
    }
    ```

- Example for utilization warning (low corridors):
  - `& "src\\VDG.CLI\\bin\\Debug\\net48\\VDG.CLI.exe" --diag-json --diag-util-warn 60 "samples\\m5_low_utilization.json" "out\\m5_low_utilization.vsdx"`
  - Expects `LowUtilization` warning in `issues[]` when middle tier has no nodes and edges span first↔last.


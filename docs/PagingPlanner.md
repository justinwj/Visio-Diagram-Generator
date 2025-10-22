# Paging Planner Cheat Sheet

The paging planner splits a diagram into printable Visio pages and reports
segmentation metrics so that large datasets stay readable and regression‐proof.

## Runtime Summary

Every CLI run prints a summary such as:

```
info: planner summary modules=210 segments=248 delta=+38 splitModules=37 avgSegments/module=1.18 pages=238 avgModules/page=1.0 avgConnectors/page=9.2 maxOccupancy=250.0% maxConnectors=48
```

Field meanings:

| Field | Description |
| --- | --- |
| `modules` | Original module count discovered from node metadata. |
| `segments` | Height-bounded module segments after splitting. |
| `delta` | `segments - modules`; positive values indicate splits. |
| `splitModules` | Number of original modules that produced more than one segment. |
| `avgSegments/module` | `segments / modules`; indicates how aggressively segmentation was applied. |
| `pages` | Page plans emitted by the paging algorithm. |
| `avgModules/page`, `avgConnectors/page` | Useful density metrics. |
| `maxOccupancy`, `maxConnectors` | Peak page utilisation and connectors per page. |
| `overflowPages`, `overflowLanes`, `overflowContainers` | Added when diagnostics detect overflow situations. |
| `crowdedPages`, `crowdedLanes` | Added when pages/lanes breach the warning threshold. |
| `connectorOverLimitPages` | Count of pages that exceeded the configured connector limit. |
| `pagesWithPartialRender` | Number of pages that required mitigation (crowding downgrade, connector deferral, etc.). |
| `truncatedNodes` | Total nodes flagged as too tall for the usable height; these require manual attention. |
| `skippedModules` | Modules that were filtered out (e.g., via `--modules` or `--max-pages`). |
| `partialRender` | Indicates the run completed with mitigated overflow (render succeeded but exceeded hard thresholds). |

## JSON Diagnostics

When `--diag-json` is enabled the same metrics are written to the diagnostics
payload:

- `metrics.moduleCount`
- `metrics.segmentCount`
- `metrics.segmentDelta`
- `metrics.splitModuleCount`
- `metrics.averageSegmentsPerModule`
- `metrics.plannerPageCount`
- `metrics.plannerAverageModulesPerPage`
- `metrics.plannerAverageConnectorsPerPage`
- `metrics.plannerMaxOccupancyPercent`
- `metrics.plannerMaxConnectorsPerPage`
- `metrics.connectorOverLimitPageCount`
- `metrics.pagesWithPartialRender`
- `metrics.truncatedNodeCount`
- `metrics.skippedModules[]` (sorted)
- `metrics.pages[] { index, name?, nodes, connectors, skippedConnectors, overflow, connectorLimit?, connectorOverLimit, moduleCount, partial, laneCrowdingWarnings, laneCrowdingErrors, pageCrowding, pageOverflow, truncatedNodes }`

These values give CI and fixture baselines a deterministic snapshot. Whenever
the planner heuristics change, record the new numbers in the fixture log so
regressions can be detected quickly.

Per-page annotations (`warning: page 12 ...`) are emitted when a page crosses connector caps, crowding limits, or incurs truncations. The metrics payload mirrors those fields so tests and CI runs can assert on the exact set of degraded pages without parsing console output.

## Splitting Heuristics (Quick Reference)

- Segmentation triggers when:
  - The module has more than six nodes, **and**
  - Either layout span exceeds `ModuleSplitThresholdMultiplier * usablePageHeight`
    or the module contains more than ~50 nodes when no layout information is available.
- Each segment is bounded to the usable page height (clamped) and capped by
  `ModuleSplitMaxSegments` (currently 24) and a node-based limit (`nodes / 20`).
- Nodes without layout coordinates are distributed evenly so connector totals stay consistent.

Adjusting the thresholds:

| Setting | Location | Effect |
| --- | --- | --- |
| `ModuleSplitThresholdMultiplier` | `Program.cs` | Scale factor applied to usable page height before splitting. |
| `ModuleSplitMaxSegments` | `Program.cs` | Hard cap on segments for a module. |
| Node limit formula | `ExpandModuleStatistics` | Determines how many segments are produced based on node count. |

Tune these constants cautiously and always re-run `./tools/render-fixture.ps1` to
refresh baselines after intentional planner changes.


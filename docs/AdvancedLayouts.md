# Advanced Layout & Review Workflow

Advanced layout mode augments the default Visio output with semantic-aware lane behaviour, bundled flows, and reviewer cues that surface directly inside each page. This document captures how to enable the mode, interpret the visual hints, and keep fixtures/review hashes deterministic.

## Enabling Advanced Mode

Use the CLI switches when running `ir2diagram` or the Visio runner:

| Option | Description |
| --- | --- |
| `--layout-advanced-mode <true|false>` | Master switch; when `true`, the planner emits lane segments, flow bundles, and cycle clusters. |
| `--layout-lane-soft-limit <int>` | Soft cap (per tier) before sub-lane overflow bands are created. |
| `--layout-lane-hard-limit <int>` | Hard cap that forces pagination once exceeded. |
| `--layout-flow-bundle-threshold <int>` | Minimum connector count before a flow bundle badge is emitted. |

Every switch has an environment-variable twin so CI/pipelines can toggle behaviour without touching command lines:

```
VDG_LAYOUT_ADVANCED_MODE=true
VDG_LAYOUT_LANE_SOFT_LIMIT=6
VDG_LAYOUT_LANE_HARD_LIMIT=10
VDG_LAYOUT_FLOW_BUNDLE_THRESHOLD=3
```

Fixtures that ship in this repository currently opt-in as follows:

| Fixture | Modes with Advanced Mode |
| --- | --- |
| `invSys` | `callgraph`, `module-structure`, `event-wiring`, `proc-cfg` (via per-mode overrides) |
| Other canonical fixtures (`hello_world`, `cross_module_calls`, `events_and_forms`) | Legacy defaults (advanced mode may be toggled per run using overrides above) |

## Visual Cues & Legend

When advanced mode is active, the Visio renderer consumes the planner metadata and adds visual markers:

* **Lane heat bands** – narrow coloured bars to the left of each tier show relative occupancy (green = healthy, yellow/orange = nearing capacity, red = over limit). Overflow reason badges (e.g., `module-soft-limit`) appear on the right edge when a sub-lane exceeded soft limits.
* **Flow bundle badges** – when many connectors share the same role transition, they are bundled into a single connector labelled `xN`. Hovering the badge in Visio shows the first few labels.
* **Cycle cluster callouts** – legends highlight strongly connected components; look for entries such as `Cycle warning: ModuleA, ModuleB…`.
* **Page legend** – every page now includes a “Reviewer cues” table near the top-right margin summarising lane pressure, bundled flows, and cycle clusters for that page along with cue severity.

### Reviewer Mapping

| Cue | Meaning | Suggested Action |
| --- | --- | --- |
| Heat band ≥ 105% | Lane is running hot; consider adjusting tiers, adding pagination overrides, or injecting seed metadata to rebalance. | Flag in `.review` summary and consider splitting tier. |
| Overflow badge (`module-soft-limit`, `node-soft-limit`, etc.) | Planner had to create sub-lanes or compress modules to stay on page. | Inspect affected modules; they will appear in `.review.json` under `review.complexity.events`. |
| Flow bundle entry | High-volume channel between tiers (e.g., Validator→Persistence). | Check `.review.txt` for matching suggestion; consider summarising flows in documentation. |
| Cycle legend entry | Mutual recursion detected. | Expand the `cycleClusters` metadata in the diagram JSON (or `.review.json`) to enumerate full membership. |

## Fixture Ledger & Hashes

`tools/render-fixture.ps1 -Update` now records review hashes alongside the existing IR/diagram/diagnostics/VSDX hashes. The ledger (`plan docs/fixtures_log.md`) contains two additional columns:

* **ReviewJson SHA256** – hash of `<fixture>.<mode>.review.json`
* **ReviewTxt SHA256** – hash of `<fixture>.<mode>.review.txt`

Older ledger entries (pre–2025‑11‑10) do not contain these hashes; they remain blank so history is preserved. Every time goldens are regenerated the script:

1. Copies `.review.json/.review.txt` into `tests/fixtures/render/**`
2. Updates `plan docs/fixtures_log.md` and `plan docs/fixtures_metadata.json` with the review hashes and relative paths
3. (Optionally) runs `tools/summarize-reviews.ps1` to refresh `plan docs/review_dashboard.md`

## Reviewer Onboarding

When auditing advanced layouts or fixtures:

1. **Run the CLI** with advanced flags (or set the `VDG_LAYOUT_*` environment variables).
2. **Open the `.review.txt`** counterpart next to each diagram to read the same cues that appear in the legend; the file header lists the active thresholds so suppressed findings are obvious.
3. **Validate hashes** by running `dotnet test tests/VDG.VBA.CLI.Tests`; the snapshot test now ensures both review artifacts match the ledger.
4. **Audit fixtures** by diffing `tests/fixtures/render/**` and confirming that review files show the expected cues; the summary dashboard in `plan docs/review_dashboard.md` spotlights any warnings/errors across fixtures.

For quick reference, the CLI help (`vdg.cli --help`) lists the new layout switches, while this document explains how to interpret the resulting glyphs and ledger entries.

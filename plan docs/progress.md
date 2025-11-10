## 2025-10-29
- Reworked `ViewModePlanner` to emit segment-aware layouts: nodes map to capped module segments, page plans drive placement, and cross-page bridges now report segment IDs so CLI can render stubs accurately.
- Added focused algorithm tests validating segmented pagination and page-bridge generation to guard the refactor.
- Surfaced node→segment assignments in the layout plan and taught the CLI to use them for page diagnostics/rendering; updated CLI tests to reflect segmentation-driven truncation behaviour.
- Captured per-page origins and dimensions in the layout plan, resetting page-local cursors so each page now exports a clean coordinate space; CLI metadata now advertises `layout.view.pageLayouts.json` for downstream renderers/tests.
- Updated Visio pagination to honour the new page origins: the CLI offsets connectors, lane containers, and bridge stubs using the page-local layout coordinates so multi-page renders no longer drift between pages.
- Expanded the planner’s node width heuristics so long labels widen cards instead of wrapping; added a regression test proving containers grow beyond the old 1.35″ baseline when labels demand it.

## 2025-10-30
- Relaxed the view-mode layout: tiers now spill into capacity-capped rows, spacing scales with connector density, and self-call corridors keep intra-module edges out of the card interior.
- Strengthened runner fallbacks to honour planner geometry; plain polylines detour around module bounds and reuse stubbed callouts instead of glueing to Visio connectors.
- Captured a `view_mode_algorithm_strategy.md` plan doc outlining the next wave of F# work (capacity rules, corridor metadata, cross-page bridges, testing focus).
- Regenerated `invSys` artefacts and reran `dotnet test` to confirm the refactor stays green and produces reproducible diagnostics bundles.

## 2025-11-09
- Landed the semantic review reporter in `VDG.VBA.CLI`: `ir2diagram` now classifies modules/roles, surfaces summaries in the console, injects the JSON payload into diagram metadata, and emits `.review.txt/.review.json` beside every diagram export for reviewer hand-off.
- Added deterministic semantics timestamps (hash-derived unless an explicit `--semantics-generated-at`/`VDG_SEMANTICS_GENERATED_AT` override is provided) so fixture refreshes stay reproducible; `tools/render-fixture.ps1` pins the override during golden regeneration.
- Updated `VDG.CLI` diagnostics to carry the `ReviewSummary` block straight from diagram metadata, ensuring downstream tooling/CI can read semantic warnings directly from `*.diagnostics.json`.
- Refreshed all fixtures (`hello_world`, `cross_module_calls`, `events_and_forms`, `invSys`) plus the sample callgraph diagram/taxonomy/flows so the new metadata, review reports, and deterministic hashes are captured in-source; both CLI and algorithms test suites are green with the new goldens.

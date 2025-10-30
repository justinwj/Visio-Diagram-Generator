## 2025-10-29
- Reworked `ViewModePlanner` to emit segment-aware layouts: nodes map to capped module segments, page plans drive placement, and cross-page bridges now report segment IDs so CLI can render stubs accurately.
- Added focused algorithm tests validating segmented pagination and page-bridge generation to guard the refactor.
- Surfaced node→segment assignments in the layout plan and taught the CLI to use them for page diagnostics/rendering; updated CLI tests to reflect segmentation-driven truncation behaviour.
- Captured per-page origins and dimensions in the layout plan, resetting page-local cursors so each page now exports a clean coordinate space; CLI metadata now advertises `layout.view.pageLayouts.json` for downstream renderers/tests.
- Updated Visio pagination to honour the new page origins: the CLI offsets connectors, lane containers, and bridge stubs using the page-local layout coordinates so multi-page renders no longer drift between pages.
- Expanded the planner’s node width heuristics so long labels widen cards instead of wrapping; added a regression test proving containers grow beyond the old 1.35″ baseline when labels demand it.

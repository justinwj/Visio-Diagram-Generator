# View-Mode Presentation Strategy

## Why This Is Needed
- Visio’s routing engine collapses connectors back onto module borders unless we provide pre-spaced corridors and avoid glue points. The current F# planner emits dense, lane-wide blocks that leave the runner no usable geometry.
- Rows/tiers inherit 10–40 modules, causing thousands of edges to originate from the same channel; Visio rejects most of them and the remainder overlap badly.
- Same-module edges still compete with cross-module corridors because there is no notion of reserved intra-module channels or edge bundling metadata.
- We skip ~11k connectors per render; diagnostics are meaningless until the planner makes pagination and corridor choices deterministic.

## Strategic Goals
1. **Deterministic Spatial Allocation** – The planner must assign modules to pages, tiers, and rows with hard capacity caps so no band exceeds a target node/connector count.
2. **Channel-First Routing** – Compute routing corridors (horizontal & vertical) per module pair plus intra-module loops. Expose these as explicit points in the layout payload so the runner only paints them.
3. **Adaptive Module Geometry** – Card width/height should depend on connector fan-out, not fixed metadata. Nodes within a card need offsets that match the corridor plan.
4. **Lossless Pagination** – If connectors cross pages, emit bridge metadata (entry/exit anchors, labels) instead of dropping edges. Diagnostics should confirm zero skipped connectors for baseline datasets.
5. **Runner Simplicity** – Keep C# focused on painting shapes and honoring metadata. All layout intelligence lives in F# so we can unit test it.

## Planner Work Breakdown
### 1. Capacity-Driven Placement
- Introduce per-tier and per-row limits (modules, nodes, connectors). When over capacity, split into additional rows or new pages _before_ slotting begins.
- Feed paging planner with connector density metrics so it can spread “hot” modules across pages.
- Track row heights and cumulative offsets to give each row a stable baseline; record this in the layout payload for diagnostics.

### 2. Corridor & Bundle Planning
- For every module pair, cluster edges by source/target anchors and create corridor descriptors: direction, centerline, width, max bundle size.
- Reserve intra-module corridors (top/bottom or left/right) using module-specific counters so self-call edges never fall back to card centers.
- Emit per-bundle offsets in the layout JSON (e.g. `routing.channels` with `center`, `width`, `bundleIndex`) for the runner.

### 3. Adaptive Module Cards
- Compute node grid parameters from both node count and outgoing bundle count; widen cards or introduce column breaks when fan-out exceeds thresholds.
- Stagger node attachment points within each card to line up with the corridor list (e.g. map connector bundles to attachment slots).
- Persist overflow metadata so that truncated nodes are reproducible (badge text, counts) while we refine visual summarization.

### 4. Cross-Page Bridges
- When a connector leaves the current page tier, emit bridge metadata (`entryAnchor`, `exitAnchor`, label, bundle id). Runner uses this to draw stubs and callouts on both pages.
- Diagnostics should track remaining cross-page bundles and enforce zero silent skips.

### 5. Testing & Tooling
- Add F# tests for lane spillover, bundle generation, and corridor spacing to guarantee deterministic output.
- Capture a set of snapshot fixtures (JSON-only) for representative VBA inputs, validating there are no skipped connectors and layout bounds remain within target page sizes.

## Immediate Engineering Steps
1. Formalize capacity constants in F# (`layout.view.maxModulesPerLane`, `layout.view.maxConnectorsPerRow`, etc.) and enforce them during segment splitting.
2. Design a corridor descriptor type (`EdgeChannel`?) and thread it through `EdgeRoute` so the runner can distinguish planner-guided paths from fallbacks.
3. Update spillover logic to record row-level offsets, ensuring future runner spacing uses planner data instead of heuristics.
4. Begin writing unit tests for `computeViewLayout` focusing on module spillover and channel emission.

## Open Questions
- What thresholds keep diagrams readable for 1k+ connectors? Need empirical data from invSys and other fixtures.
- How should we order modules within tiers when capacity forces wrap-around? (Alphabetical? Connector-driven?)
- Do we need separate strategies for “screen” vs “print” modes, or can we parameterize the same planner with different caps?

## Action Items
- [ ] Encode capacity limits and enforce multi-row placements in F#.
- [ ] Prototype `EdgeChannel` metadata and wire it to the runner.
- [ ] Capture updated diagnostics showing zero skipped connectors for invSys once spillover/bundling is in place.
- [ ] Expand test suite to lock in corridor computations and pagination decisions.
***
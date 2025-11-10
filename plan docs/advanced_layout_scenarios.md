# Advanced Layout & Flow Scenario Enhancements

## Current Constraints & Pain Points
- **Dense subsystems collapse lanes** – once a subsystem crosses the `LaneCapacity` threshold we paginate, but pagination is binary and rarely aligns with reviewer expectations (e.g., UI-heavy tiers still overlap in page 1).
- **Highly connected modules produce spaghetti connectors** – cross-tier calls and fan-in/fan-out flows turn into overlapping splines even with current semantic channel labeling.
- **Cycles & mutual recursion** – current planner flattens strongly connected components but does not expose cycle context, making troubleshooting difficult.
- **No distinction between "must stay together" vs "can split" groups** – heuristics treat all semantic clusters the same; teams want ownership pods or transaction bundles to stay co-located even if heavy.
- **Limited fallback cues** – when we hit irreducible complexity we only emit warnings in CLI; the diagram itself offers few hints (no minimap, legend callouts, or collapsed overlays).

## Goals
1. Keep diagrams reviewer-friendly even for large call graphs by adapting lane allocation, pagination, and bundling.
2. Surface complexity explicitly (in-diagram and via review metadata) so reviewers can focus on hot spots.
3. Maintain deterministic, seed-aware output; advanced behaviors must be reproducible across machines/CI.

## Proposed Enhancements

### 1. Hierarchical Lane Splitting & Adaptive Buckets
- **Soft vs. hard capacity**: add per-tier thresholds (e.g., soft limit 8, hard limit 12). Soft overflow triggers *sub-lane* creation before we open a new page.
- **Semantic-aware clustering**: keep ownership pods (from seed `metadata.grouping = "cohesive"`) together during first split; less-related modules can be moved to new sub-lanes.
- **Implementation sketch**:
  - Extend `ViewModePlanner.ComputeViewLayout` to introduce `LaneCluster` objects (`LaneCluster.Id`, `Tier`, `Modules`, `Weight`).
  - When `weight > softLimit`, spawn `LaneSegment` objects that remain on the same page but get offset bands (visual shading or label suffix “(overflow)”).
  - Once `weight > hardLimit`, escalate to pagination (existing behavior) but keep the overflow rationale in metadata.

### 2. Flow Bundling & Hierarchical Edges
- **Channel bundling**: detect repeated edges between the same tier/role pair and render a single connector with `n` badge plus tooltip listing underlying calls.
- **Recursive clusters**: collapse strongly connected components into a meta-node labeled “Cycle: <names>”; provide expand metadata for downstream portals.
- **Edge routing tiers**: assign connectors to semantic “channels” (UI↔Service, Service↔Persistence, etc.) and maintain per-channel spacing to reduce overlaps.

### 3. Adaptive Pagination & Focus Views
- **Smart pagination order**: keep critical tiers (Validator/Service) on page 1, push accessory subsystems to later pages with “See Page 2 for Integrations” label.
- **Context mini-legend**: each page embeds a mini-table summarizing what moved off-page (counts + reason).
- **Focus view metadata**: emit `view.focus` hints per diagram, enabling CLI/portal consumers to jump to relevant page automatically.

### 4. Fallback Visual Cues
- **Collapse overlays**: when we collapse nodes or bundles, stamp a filled glyph plus textual note “Collapsed 5 helper modules; see review summary for full list”.
- **Heat bands**: shading intensity tied to semantic “heaviness” or lane pressure so dense areas are obvious even without reading warnings.
- **Reviewer hooks**: propagate all overflow/bundling decisions into review JSON (`review.complexity.events`) for dashboards.

## Implementation Plan
1. **Planner refactor scaffolding**
   - Introduce `AdvancedLayoutOptions` (soft/hard limits, bundling toggles) with defaults derived from current constants.
   - Split `ViewModePlanner` into discrete phases: `ClusterSemantics -> AllocateLanes -> RouteFlows -> EmitAnnotations`.
2. **Lane splitting prototype**
   - Implement `LaneCluster` + `LaneSegment` data structures with deterministic ordering (primary sort by subsystem, then module name).
   - Update layout builder to draw sub-lanes (adjust Y offsets and swimlane headers).
3. **Flow bundling**
   - Add `FlowBundle` aggregator keyed by `(sourceTier, targetTier, roleTransition)`; route aggregated connector with badge counts.
   - Provide per-edge metadata listing underlying calls for diagnostics/goldens.
4. **Pagination enhancements**
   - Add `PageContext` metadata capturing why a module moved (overflow, dependency isolation, cycle).
   - Ensure CLI text summary references same context (e.g., “Page 2 created for Integrations (overflow: +6 modules)”).
5. **Fallback cues**
   - Extend renderer to draw collapse glyphs and heat bands (initially simple shading; future gradient palette).
   - Include same info in `.review.json` for portals.

## Golden & Fixture Strategy
- **New fixtures**:
  - `dense_ui_form.seed.json` + IR capturing UI-heavy tier → exercise sub-lanes.
  - `cycle_hotspot` scenario with recursive helpers to validate cycle collapsing.
  - `fanout_integrations` scenario to test flow bundling/badges.
- **Outputs**:
  - Diagram JSONs showing `laneSegments`, `flowBundles`, `pageContexts`.
  - Review summaries referencing overflow rationale.
- Ensure deterministic timestamps/ordering by reusing existing deterministic clock hooks.

## Testing Matrix
1. **Unit tests** (e.g., `AdvancedLayoutPlannerTests`):
   - Lane splitting respects soft/hard limits.
   - Bundles aggregate connectors while preserving metadata.
   - Cycle detection collapses strongly connected components deterministically.
2. **Smoke/golden tests**:
   - `ParserSmokeTests` verifying new fixtures match expectations.
   - CLI integration tests ensuring review summary lists new cues.
3. **Regression**:
   - Existing fixtures without advanced scenarios remain unchanged (compares hash).
   - Backcompat toggles (env `VDG_LAYOUT_ADVANCED=0`) keep old behavior for CI bisecting.

## Documentation & Review Surfacing
- Create `docs/AdvancedLayouts.md` with before/after diagrams + reviewer guidance.
- Update CLI help to mention new options (`--layout-advanced-mode`, `--layout-soft-limit`, etc.).
- Mirror overflow/cycle data into `.review.txt` and diagnostics JSON for dashboards.

## Open Questions / Next Review Checkpoints
- Exact soft/hard limit defaults per tier (start with 6/10? calibrate via fixtures).
- Whether to allow user-provided bundling rules (seed metadata `groupFlowsBy`).
- Visual affordances for collapse glyphs in Visio vs. CLI-only output – may need designer sign-off.
- Timeline for enabling by default vs. opt-in preview flag.
